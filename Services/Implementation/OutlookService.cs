using CVexplorer.Data;
using CVexplorer.Models.Domain;
using CVexplorer.Models.DTO;
using CVexplorer.Repositories.Implementation;
using CVexplorer.Repositories.Interface;
using CVexplorer.Services.Interface;
using iText.Commons.Bouncycastle.Cert.Ocsp;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using System.Net.Http.Headers;

namespace CVexplorer.Services.Implementation
{
    public class OutlookService (ILogger<OutlookService>_logger,ICVRepository _cvRepository,UserManager<Models.Domain.User> _userManager, IConfiguration _config, DataContext _context, IRoundRepository _roundRepository) : IOutlookService
    {

        public class TokenResult
        {
            public string AccessToken { get; set; } = null!;
            public string RefreshToken { get; set; } = null!;
            public DateTimeOffset ExpiresAt { get; set; }
        }
        public async Task<TokenResult> GetOrRefreshTokensAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId)
                       ?? throw new Exception("User does not exist");



            const string provider = "Microsoft";

            
            var accessToken = await _userManager.GetAuthenticationTokenAsync(user, provider, "access_token");
            var refreshToken = await _userManager.GetAuthenticationTokenAsync(user, provider, "refresh_token");
            var expiresAtStr = await _userManager.GetAuthenticationTokenAsync(user, provider, "expires_at");

            if (string.IsNullOrEmpty(accessToken)
             || string.IsNullOrEmpty(refreshToken)
             || string.IsNullOrEmpty(expiresAtStr)
             || !long.TryParse(expiresAtStr, out var expiresAtUnix))
            {
                throw new InvalidOperationException("Stored tokens are missing or invalid.");
            }

            var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expiresAtUnix);

            // 3) Dacă accesul e expirat, facem refresh
            if (expiresAt <= DateTimeOffset.UtcNow)
            {
                
                var clientId = _config["Microsoft:AzureAd:ClientId"] ?? throw new Exception("ClientId missing");
                var clientSecret = _config["Microsoft:AzureAd:ClientSecret"] ?? throw new Exception("ClientSecret missing");
                var tenantId = _config["Microsoft:AzureAd:TenantId"] ?? throw new Exception("TenantId missing");
                var authority = $"https://login.microsoftonline.com/{tenantId}";

                var cca = ConfidentialClientApplicationBuilder
                            .Create(clientId)
                            .WithClientSecret(clientSecret)
                            .WithAuthority(authority)
                            .Build();

                
                var scopes = new[] { "User.Read", "Mail.Read" };
                string strRefreshToken = refreshToken.ToString();

                var refreshBuilder = ((IByRefreshToken)cca)
                       .AcquireTokenByRefreshToken(scopes, refreshToken);

                AuthenticationResult msalResult;
                try
                {
                    msalResult = await refreshBuilder.ExecuteAsync();

                }
                catch (MsalUiRequiredException)
                {
                    throw new UnauthorizedAccessException("Invalid Token . Need to Authenticate");
                }

                
                accessToken = msalResult.AccessToken;
                expiresAt = msalResult.ExpiresOn;

                await _userManager.SetAuthenticationTokenAsync(user, provider, "access_token", accessToken);
                await _userManager.SetAuthenticationTokenAsync(user, provider, "expires_at", expiresAt.ToUnixTimeSeconds().ToString());
            }

            // 5) Returnăm întotdeauna un TokenResult valid
            return new TokenResult
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = expiresAt
            };
        }

        public async Task<List<OutlookFolderListDTO>> GetFoldersAsync(string userId, TokenResult tokens, string publicPosId)
        {
            var graphClient = new GraphServiceClient(new DelegateAuthenticationProvider(async requestMessage =>
            {
                requestMessage.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
            }));

            
            var allFolders = new List<MailFolder>();
            var page = await graphClient.Me.MailFolders
                                        .Request()
                                        .GetAsync();

            allFolders.AddRange(page.CurrentPage);
            while (page.NextPageRequest != null)
            {
                page = await page.NextPageRequest.GetAsync();
                allFolders.AddRange(page.CurrentPage);
            }
            var position = _context.Positions.First(p => p.PublicId == publicPosId);

            // 4) Load your Outlook subscriptions for that position
            var existingSubs = await _context.IntegrationSubscriptions
                .Where(s =>
                    s.Provider == "Outlook" &&
                    s.UserId.ToString() == userId &&
                    s.PositionId == position.Id)
                .Select(s => s.LabelId)
                .ToListAsync();

            var subscribedIds = new HashSet<string>(existingSubs, StringComparer.OrdinalIgnoreCase);

            var result = allFolders.Select(f => new OutlookFolderListDTO
            {
                Id = f.Id,
                Name = f.DisplayName,
                isSubscribed = subscribedIds.Contains(f.Id)
            }).ToList();

            return result;
        }

        public async Task<List<OutlookFolderListDTO>> SubscribeFolders(List<string>folderIds,string userId, TokenResult tokens, string publicPosId , string? roundId = null)
        {
            
            var position = await _context.Positions
                .SingleOrDefaultAsync(p => p.PublicId == publicPosId)
                ?? throw new Exception("Position does not exist");

            var user = await _userManager.FindByIdAsync(userId)
                       ?? throw new Exception("User does not exist");

            var graphClient = new GraphServiceClient(new DelegateAuthenticationProvider(msg =>
            {
                msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
                return Task.CompletedTask;
            }));

            // 5) Luăm subscripțiile deja salvate în DB
            var existingSubs = await _context.IntegrationSubscriptions
                .Where(s => s.UserId == user.Id
                         && s.Provider == "Outlook"
                         && s.PositionId == position.Id)
                .ToListAsync();

        

            Round actualRound;
            if (!string.IsNullOrWhiteSpace(roundId))
            {
                // Dacă roundId e dat, îl încarc din baza de date
                actualRound = await _context.Rounds
                    .SingleOrDefaultAsync(r => r.PublicId == roundId)
                    ?? throw new Exception($"Round with PublicId {roundId} does not exist");
            }
            else
            {
                // Dacă nu e dat, creez unul nou
                actualRound = await _roundRepository.CreateAsync(position.Id);
            }

            var distinctFolders = folderIds.Distinct().ToArray();
            var toAdd = distinctFolders.Except(existingSubs.Select(s => s.LabelId));
            var toRemove = existingSubs.Select(s => s.LabelId).Except(distinctFolders);


            if (toRemove.Count() == existingSubs.Count && existingSubs.Count > 0)
            {
                // Șterg fiecare subscripție de pe Graph
                foreach (var sub in existingSubs)
                {
                    await graphClient
                        .Subscriptions[sub.SubscriptionName]
                        .Request()
                        .DeleteAsync();
                }

                _context.IntegrationSubscriptions.RemoveRange(existingSubs);
                existingSubs.Clear();
            }
            if (toRemove.Any())
            {
                var subsToDelete = existingSubs
                    .Where(s => toRemove.Contains(s.LabelId))
                    .ToList();

                foreach (var sub in subsToDelete)
                {
                    
                    await graphClient.Subscriptions[sub.SubscriptionName]
                                     .Request()
                                     .DeleteAsync();
                    _context.IntegrationSubscriptions.Remove(sub);
                }

                _context.IntegrationSubscriptions.RemoveRange(subsToDelete);
                existingSubs.RemoveAll(s => toRemove.Contains(s.LabelId));
            }

            
            var remainingFolders = existingSubs
                .Where(s => !toRemove.Contains(s.LabelId))
                .Select(s => s.LabelId)
                .Union(toAdd)
                .Distinct()
                .ToList();


            if (!remainingFolders.Any())
            {
                await _context.SaveChangesAsync();

                // Returnez toate folderele cu isSubscribed = false
                var allFoldersEmpty = new List<MailFolder>();
                var firstPageEmpty = await graphClient.Me.MailFolders.Request().GetAsync();
                allFoldersEmpty.AddRange(firstPageEmpty.CurrentPage);
                while (firstPageEmpty.NextPageRequest != null)
                {
                    firstPageEmpty = await firstPageEmpty.NextPageRequest.GetAsync();
                    allFoldersEmpty.AddRange(firstPageEmpty.CurrentPage);
                }

                return allFoldersEmpty
                    .Select(f => new OutlookFolderListDTO
                    {
                        Id = f.Id,
                        Name = f.DisplayName,
                        isSubscribed = false
                    })
                    .ToList();
            }


            
            
                var msUser = await graphClient.Me
                .Request()
                .Select(u => new {
                    u.Mail,
                    u.UserPrincipalName
                })
                .GetAsync();


                var email = !string.IsNullOrEmpty(msUser.Mail)
                    ? msUser.Mail
                    : msUser.UserPrincipalName;

                if (string.IsNullOrEmpty(email))
                    throw new Exception("Could not retrieve email from Microsoft Graph");

                var maxExp = DateTimeOffset.UtcNow.AddMinutes(4230); // ~72h

                foreach (var folderId in toAdd)
                {
                    var subscription = new Subscription
                    {
                        ChangeType = "created",
                        NotificationUrl = _config["Microsoft:WehHookEndpoint"]
                                         ?? throw new Exception("NotificationUrl is not configured"),
                        Resource = $"me/mailFolders('{folderId}')/messages",
                        ExpirationDateTime = maxExp,
                        ClientState = email
                    };

                    var sub = await graphClient.Subscriptions
                                               .Request()
                                               .AddAsync(subscription);

                    var integrationSub = new IntegrationSubscription
                    {
                        UserId = Convert.ToInt32(userId),
                        Provider = "Outlook",
                        LabelId = folderId,
                        PositionId = position.Id,
                        SyncToken = string.Empty,
                        Email = email,
                        ExpiresAt = sub.ExpirationDateTime.Value,
                        UpdatedAt = DateTimeOffset.UtcNow,
                        SubscriptionName = sub.Id,
                        RoundId = actualRound.Id
                    };
                    _context.IntegrationSubscriptions.Add(integrationSub);


                }
                foreach (var sub in existingSubs)
                {

                    sub.RoundId = actualRound.Id;
                    sub.UpdatedAt = DateTimeOffset.UtcNow;

                }


                await _context.SaveChangesAsync();

                var allFolders = new List<MailFolder>();
                var page = await graphClient.Me.MailFolders.Request().GetAsync();
                allFolders.AddRange(page.CurrentPage);
                while (page.NextPageRequest != null)
                {
                    page = await page.NextPageRequest.GetAsync();
                    allFolders.AddRange(page.CurrentPage);
                }

                // 11) Which folder IDs are currently subscribed?
                var subscribedIds = await _context.IntegrationSubscriptions
                    .Where(s =>
                        s.Provider == "Outlook" &&
                        s.UserId.ToString() == userId &&
                        s.PositionId == position.Id)
                    .Select(s => s.LabelId)
                    .ToListAsync();

            
                return  allFolders
                    .Select(f => new OutlookFolderListDTO
                    {
                        Id = f.Id,
                        Name = f.DisplayName,
                        isSubscribed = subscribedIds.Contains(f.Id)
                    })
                    .ToList();

        }

        public async Task ProcessNewMessageAsync(string messageId, string folderId, long subscriptionId)
        {
            
            var sub = await _context.IntegrationSubscriptions
                        .AsNoTracking()
                        .FirstOrDefaultAsync(s => s.Id == subscriptionId);
            if (sub == null)
                throw new InvalidOperationException($"No subscription for folder {folderId}");

            // 2) Obține token-urile actualizate pentru acel user
            var tokens = await GetOrRefreshTokensAsync(sub.UserId.ToString());

            // 3) Creează GraphServiceClient
            var graphClient = new GraphServiceClient(new DelegateAuthenticationProvider(req =>
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
                return Task.CompletedTask;
            }));

            // 4) Fetch mesajul cu attachments
            var msg = await graphClient.Me.Messages[messageId]
                           .Request()
                           .Select(m => new {
                               m.Id,
                               m.Subject,
                               m.ParentFolderId
                           })
                           .Expand("attachments")
                           .GetAsync();

            
            var pdfFiles = new List<IFormFile>();
            foreach (var att in msg.Attachments.OfType<FileAttachment>())
            {
                if (!att.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    continue;

                
                var bytes = att.ContentBytes;

                
                var ms = new MemoryStream(bytes);
                var file = new FormFile(ms, 0, ms.Length, "file", att.Name)
                {
                    Headers = new HeaderDictionary(),
                    ContentType = att.ContentType
                };

                pdfFiles.Add(file);
            }
            if (!pdfFiles.Any())
            {
                
                return;
            }

            // 5) Obținem publicPositionId din Position
            var publicPositionId = await _context.Positions
                .Where(p => p.Id == sub.PositionId)
                .Select(p => p.PublicId)
                .FirstOrDefaultAsync();

            int processedCVs = 0;
            foreach (var pdf in pdfFiles)
            {
                var success = await _cvRepository.UploadDocumentAsync(
                    file: pdf,
                    publicPositionId: publicPositionId!,
                    userId: sub.UserId,
                    roundId: sub.RoundId , "Outlook");

                processedCVs++;

                if (!success)
                {
                    _logger.LogWarning(
                        "UploadDocumentAsync failed to process for  {FileName}, user {UserId}, round {RoundId}",
                        pdf.FileName, sub.UserId, sub.RoundId);
                }
            }
            sub.ProcessedCVs += processedCVs;
            await _context.SaveChangesAsync();


        }


        public async Task<bool> UnsubscribeAsync(string userId, TokenResult tokens, string publicPosId)
        {
            var graphClient = new GraphServiceClient(new DelegateAuthenticationProvider(req =>
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
                return Task.CompletedTask;
            }));

            
            var deleted = new List<string>();
            var page = await graphClient.Subscriptions.Request().GetAsync();
            while (page != null)
            {
                foreach (var sub in page.CurrentPage)
                {
                    
                    await graphClient.Subscriptions[sub.Id]
                                     .Request()
                                     .DeleteAsync();
                    deleted.Add(sub.Id);
                }
                page = page.NextPageRequest != null
                    ? await page.NextPageRequest.GetAsync()
                    : null;
            }

            var position = _context.Positions
                .FirstOrDefault(p => p.PublicId == publicPosId)
                ?? throw new Exception("Position does not exist");
            

            var localSubs = await _context.IntegrationSubscriptions
                .Where(s => s.Provider == "Outlook" && s.UserId.ToString() == userId && s.PositionId == position.Id)
                .ToListAsync();
            _context.IntegrationSubscriptions.RemoveRange(localSubs);
            await _context.SaveChangesAsync();

            
            return true;
        }

        public async Task Disconnect(string userId, TokenResult tokens)
        {
            var user = await _userManager.FindByIdAsync(userId )
                       ?? throw new Exception("User does not exist");

            var graphClient = new GraphServiceClient(new DelegateAuthenticationProvider(msg =>
            {
                msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
                return Task.CompletedTask;
            }));

            
            var uid = Convert.ToInt32(userId);
          

            await _userManager.RemoveAuthenticationTokenAsync(user, "Microsoft", "access_token");
            await _userManager.RemoveAuthenticationTokenAsync(user, "Microsoft", "refresh_token");
            await _userManager.RemoveAuthenticationTokenAsync(user, "Microsoft", "expires_at");

            var subs = await _context.IntegrationSubscriptions
                .Where(s => s.UserId == user.Id && s.Provider == "Outlook")
                .ToListAsync();

            foreach (var sub in subs)
            {
                try
                {
                    await graphClient.Subscriptions[sub.SubscriptionName]
                                     .Request()
                                     .DeleteAsync();
                }
                catch
                {
                    continue;
                }
            }


            if (subs.Any())
            {
                _context.IntegrationSubscriptions.RemoveRange(subs);
                
            }

            await _context.SaveChangesAsync();
        }

        public async Task<SessionDTO> GetSessionDataAsync(string userId, string? publicId = null)
        {
            var user = await _userManager.FindByIdAsync(userId)
                       ?? throw new Exception("User does not exist");


            var expiresAtStr = await _userManager.GetAuthenticationTokenAsync(
                                   user, "Microsoft", "expires_at");

            string? expiry = null;
            if (long.TryParse(expiresAtStr, out var unix))
            {
                var dto = DateTimeOffset.FromUnixTimeSeconds(unix);
                // obținem stringul ISO 8601, ex: "2025-05-30T14:23:45.0000000Z"
                expiry = dto.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffK");
            }




            var position = await _context.Positions
                .SingleOrDefaultAsync(p => p.PublicId == publicId)
                ?? throw new Exception("Position does not exist");

            var existingSub = await _context.IntegrationSubscriptions.Include(s => s.Round).FirstOrDefaultAsync(s =>
                s.UserId == user.Id && s.Provider == "Outlook" && s.PositionId == position.Id);



            return new SessionDTO
            {
                ProcessedCVs = existingSub?.ProcessedCVs ?? 0,
                IsProcessing = existingSub != null ? true : false,
                Expiry = expiry,
                ProcessingRoundId = existingSub?.Round.PublicId ?? null,
            };

        }


    }
}
