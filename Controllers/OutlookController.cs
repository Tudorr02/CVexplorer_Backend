using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Graph;
using System.Security.Claims;
using CVexplorer.Models.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Identity.Client;
using System.Net.Http.Headers;
using Microsoft.Identity.Client.Extensions.Msal;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using CVexplorer.Data;
using CVexplorer.Repositories.Interface;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using CVexplorer.Repositories.Implementation;
using CVexplorer.Models.DTO;
using CVexplorer.Services.Interface;

namespace CVexplorer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OutlookController(IBackgroundTaskQueue _queue,ICVRepository _cvRepository,ILogger<OutlookController> _logger, UserManager<Models.Domain.User> _userManager ,IRoundRepository _roundRepository, IConfiguration _config , DataContext _context) : Controller
    {
       

        [HttpGet("login")]
        [Authorize]
        public IActionResult Login()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();


            var props = new AuthenticationProperties();
            props.Items["UserId"] = userId;
            props.RedirectUri = _config["Microsoft:RedirectUri"];

            return Challenge(props,"Microsoft");

        }

        private class TokenResult
        {
            public string AccessToken { get; set; } = null!;
            public string RefreshToken { get; set; } = null!;
            public DateTimeOffset ExpiresAt { get; set; }
        }
        private async Task<TokenResult> CheckMsTokensAsync(string userId)
        {
            // 1) Identificăm user-ul curent
            var user = await _userManager.FindByIdAsync(userId)
                       ?? throw new Exception("Utilizator inexistent");

            

            const string provider = "Microsoft";

            // 2) Citim token-urile stocate
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
                // Configurarea MSAL
                var clientId = _config["Microsoft:AzureAd:ClientId"] ?? throw new Exception("ClientId missing");
                var clientSecret = _config["Microsoft:AzureAd:ClientSecret"] ?? throw new Exception("ClientSecret missing");
                var tenantId = _config["Microsoft:AzureAd:TenantId"] ?? throw new Exception("TenantId missing");
                var authority = $"https://login.microsoftonline.com/{tenantId}";

                var cca = ConfidentialClientApplicationBuilder
                            .Create(clientId)
                            .WithClientSecret(clientSecret)
                            .WithAuthority(authority)
                            .Build();

                // Scope-urile folosite în app-settings
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
                    throw new UnauthorizedAccessException("Refresh token invalid sau expirat. Este necesară re-autentificarea.");
                }

                // 4) Salvăm valorile noi
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


        [HttpGet("session")]
        [Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{"MicrosoftCookie"}")]

        public async Task<IActionResult> Session()
        {
            var jwtResult = await HttpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
            //var cookieResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            var cookieResult = await HttpContext.AuthenticateAsync("MicrosoftCookie");

            if (!jwtResult.Succeeded || !cookieResult.Succeeded)
                return Forbid();

            var jwtUserId = jwtResult.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var cookieUserId = cookieResult.Properties.Items["UserId"];

            if (jwtUserId == null || cookieUserId == null || jwtUserId != cookieUserId)
                return Forbid();


            return Ok();
        }


        [HttpGet("folders")]
        [Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{"MicrosoftCookie"}")]
        public async Task<IActionResult> GetFolders()
        {
            var jwtResult = await HttpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
            //var cookieResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            var cookieResult = await HttpContext.AuthenticateAsync("MicrosoftCookie");

            if (!jwtResult.Succeeded || !cookieResult.Succeeded)
                return Forbid();

            var jwtUserId = jwtResult.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var cookieUserId = cookieResult.Properties.Items["UserId"];

            if (jwtUserId == null || cookieUserId == null || jwtUserId != cookieUserId)
                return Forbid();

            // 1) Obținem token-urile (va face refresh dacă e nevoie)
            var tokens = await CheckMsTokensAsync(jwtUserId);

            // 2) Construim GraphServiceClient cu access token-ul
            var graphClient = new GraphServiceClient(new DelegateAuthenticationProvider(async requestMessage =>
            {
                requestMessage.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
            }));

            // 3) Colectăm toate folderele (pagini)
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

            // 4) Returnăm lista de foldere
            return Ok(allFolders);
        }

        [HttpPost("subscribe-folders")]
        [Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{"MicrosoftCookie"}")]
        public async Task<IActionResult> SubscribeFolders([FromBody] SubscribeRequest req)
        {
            // 1) Authenticate ca în celelalte metode
            var jwtResult = await HttpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
            var cookieResult = await HttpContext.AuthenticateAsync("MicrosoftCookie");
            if (!jwtResult.Succeeded || !cookieResult.Succeeded)
                return Forbid();

            var userId = jwtResult.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var cookieId = cookieResult.Properties.Items["UserId"];
            if (userId == null || cookieId == null || userId != cookieId)
                return Forbid();

            // 2) Găsim poziția
            var position = await _context.Positions
                .SingleOrDefaultAsync(p => p.PublicId == req.PositionPublicId)
                ?? throw new Exception("Poziție inexistentă");

            // 3) Găsim user-ul
            var user = await _userManager.FindByIdAsync(userId)
                       ?? throw new Exception("Utilizator inexistent");

            // 4) Refresh/access token
            var tokens = await CheckMsTokensAsync(userId);
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

            // 6) Dacă nu exista niciun subscription, creăm o rundă nouă
            Round initialRound = null;
            if (!existingSubs.Any())
                initialRound = await _roundRepository.CreateAsync(position.Id);

            // 7) Calculăm ce folderIds trebuie adăugate și șterse
            var toAdd = req.FolderIds.Distinct().Except(existingSubs.Select(s => s.LabelId));
            var toRemove = existingSubs.Select(s => s.LabelId).Except(req.FolderIds.Distinct());

            // 8) DELETE pe Graph și în DB pentru subs care trebuie eliminate
            if (toRemove.Any())
            {
                var subsToDelete = existingSubs
                    .Where(s => toRemove.Contains(s.LabelId))
                    .ToList();

                foreach (var sub in subsToDelete)
                {
                    // --- Aici: ștergem subscription-ul din Graph ---
                    await graphClient.Subscriptions[sub.SubscriptionName]
                                     .Request()
                                     .DeleteAsync();

                    // apoi din context
                    _context.IntegrationSubscriptions.Remove(sub);
                }
            }

            // 9) Dacă n-au rămas labels, terminăm
            var remainingLabels = existingSubs
                .Where(s => !toRemove.Contains(s.LabelId))
                .Select(s => s.LabelId)
                .Union(toAdd)
                .Distinct()
                .ToList();

            if (!remainingLabels.Any())
            {
                await _context.SaveChangesAsync();
                return Ok(new { RequestedFolders = req.FolderIds.Distinct() });
            }

            var msUser = await graphClient.Me
                .Request()
                .Select(u => new {
                    u.Mail,
                    u.UserPrincipalName
                })
                .GetAsync();

            //  →  Folosim Mail dacă există, altfel UserPrincipalName
            var email = !string.IsNullOrEmpty(msUser.Mail)
                ? msUser.Mail
                : msUser.UserPrincipalName;

            if (string.IsNullOrEmpty(email))
                throw new Exception("Could not retrieve email from Microsoft Graph");

            var maxExp = DateTimeOffset.UtcNow.AddMinutes(4230); // ~72h
            var createdList = new List<object>();
            foreach (var folderId in toAdd)
            {
                var subscription = new Subscription
                {
                    ChangeType = "created",
                    NotificationUrl = "https://mint-lionfish-evidently.ngrok-free.app/api/outlook/notifications",
                    Resource = $"me/mailFolders('{folderId}')/messages",
                    ExpirationDateTime = maxExp,
                    ClientState = email
                };

                var sub = await graphClient.Subscriptions
                                           .Request()
                                           .AddAsync(subscription);

                var integrationSub = new IntegrationSubscription
                {
                    UserId = user.Id,
                    Provider = "Outlook",
                    LabelId = folderId,
                    PositionId = position.Id,
                    SyncToken = string.Empty,                  // Graph nu-ți dă historyId
                    Email = email,
                    ExpiresAt = sub.ExpirationDateTime.Value,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    SubscriptionName = sub.Id,
                    RoundId = initialRound != null
                                         ? initialRound.Id
                                         : existingSubs.First().RoundId
                };
                _context.IntegrationSubscriptions.Add(integrationSub);

                createdList.Add(new
                {
                    sub.Id,
                    sub.Resource,
                    sub.NotificationUrl,
                    sub.ExpirationDateTime
                });
            }

            // 11) Salvăm totul dintr-o dată
            await _context.SaveChangesAsync();

            // 3) Get _all_ current subs to show in the response
            var allSubs = (await graphClient.Subscriptions.Request().GetAsync())
                          .CurrentPage
                          .Select(s => new {
                              s.Id,
                              s.Resource,
                              s.NotificationUrl,
                              s.ExpirationDateTime
                          });

            return Ok(new
            {
                RequestedFolders = req.FolderIds.Distinct(),
                CreatedSubscriptions = createdList,
                AllSubscriptions = allSubs
            });
        }

        [HttpPost("notifications")]
        [AllowAnonymous]
        public async Task<IActionResult> Notifications()
        {
            // 1) Validation request: echo back the token in plain text
            if (Request.Query.TryGetValue("validationToken", out var validationTokens))
            {
                var token = validationTokens.FirstOrDefault();
                if (!string.IsNullOrEmpty(token))
                    return Content(token, "text/plain");
            }

            // 2) Read body
            string json;
            using (var reader = new StreamReader(Request.Body))
            {
                json = await reader.ReadToEndAsync();
            }

            // 3) Deserialize
            NotificationCollection notifications;
            try
            {
                notifications = JsonSerializer.Deserialize<NotificationCollection>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? throw new Exception("Invalid payload");
            }
            catch (Exception ex)
            {
                // bad JSON
                _logger.LogWarning( "Unable to parse notification payload {Msg}",ex.Message );
                return Accepted();
            }

            Accepted();

            // 4) Enqueue each notification as a PushJobDTO
            foreach (var note in notifications.Value)
            {
                // găsim sub-ul doar pentru a lua LabelId
                var sub = await _context.IntegrationSubscriptions
                            .AsNoTracking()
                            .FirstOrDefaultAsync(s => s.SubscriptionName == note.SubscriptionId);

                if (sub == null)
                {
                    _logger.LogWarning("Subscription {SubId} not found, skipping", note.SubscriptionId);
                    continue;
                }

                var job = new PushJobDTO
                {
                    Provider = "Outlook",
                    SubscriptionId = note.SubscriptionId,
                    ResourceId = sub.LabelId,
                    MessageId = note.ResourceData.Id
                };

                await _queue.EnqueueAsync(job);
            }

            // 5) Răspundem imediat ca să nu cauzeze retry
            return StatusCode(StatusCodes.Status202Accepted);
        }

        public class NotificationCollection
        {
            public List<Notification> Value { get; set; } = new();
        }
        public class Notification
        {
            public string SubscriptionId { get; set; } = "";
            public string ChangeType { get; set; } = "";
            public string Resource { get; set; } = "";
            public ResourceData ResourceData { get; set; } = new();
            public string ClientState { get; set; } = "";
        }
        public class ResourceData
        {
            public string Id { get; set; } = "";
            public string ODataType { get; set; } = "";
            public string ODataId { get; set; } = "";
        }
        public class SubscribeRequest
        {
            public List<string> FolderIds { get; set; } = new();
            public string NotificationUrl { get; set; } = "";   // ex: https://exemplu.com/api/outlook/notifications
            public string ClientState { get; set; } = "";   // un secret scurt, ex GUID;
            public string PositionPublicId { get; set; }    // publicId-ul poziției
        }

        [NonAction]
        public async Task ProcessNewMessageAsync(string messageId, string folderId)
        {
            // 1) Găsește subscripția locală ca să afli UserId
            var sub = await _context.IntegrationSubscriptions
                        .AsNoTracking()
                        .FirstOrDefaultAsync(s => s.Provider == "Outlook"
                                               && s.LabelId == folderId);
            if (sub == null)
                throw new InvalidOperationException($"No subscription for folder {folderId}");

            // 2) Obține token-urile actualizate pentru acel user
            var tokens = await CheckMsTokensAsync(sub.UserId.ToString());

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

            // 5) Iterează atașamentele de tip FileAttachment și PDF
            var pdfFiles = new List<IFormFile>();
            foreach (var att in msg.Attachments.OfType<FileAttachment>())
            {
                if (!att.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Conținutul e deja în ContentBytes
                var bytes = att.ContentBytes;

                // 6) Creăm un MemoryStream și un IFormFile
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
                // nu e PDF, nu facem nimic
                return;
            }

            // 5) Obținem publicPositionId din Position
            var publicPositionId = await _context.Positions
                .Where(p => p.Id == sub.PositionId)
                .Select(p => p.PublicId)
                .FirstOrDefaultAsync();

            // 6) Apelează UploadDocumentAsync pentru fiecare PDF
            foreach (var pdf in pdfFiles)
            {
                var success = await _cvRepository.UploadDocumentAsync(
                    file: pdf,
                    publicPositionId: publicPositionId!,
                    userId: sub.UserId,
                    roundId: sub.RoundId);

                if (!success)
                {
                    _logger.LogWarning(
                        "UploadDocumentAsync a returnat false pentru {FileName}, user {UserId}, round {RoundId}",
                        pdf.FileName, sub.UserId, sub.RoundId);
                }
            }

           
        }

        [HttpPost("clear-subscriptions")]
        [Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},MicrosoftCookie")]
        public async Task<IActionResult> ClearSubscriptions()
        {
            // 1) Authenticate
            var jwtResult = await HttpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
            var cookieResult = await HttpContext.AuthenticateAsync("MicrosoftCookie");
            if (!jwtResult.Succeeded || !cookieResult.Succeeded)
                return Forbid();

            var userId = jwtResult.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var cookieId = cookieResult.Properties.Items["UserId"];
            if (userId == null || cookieId == null || userId != cookieId)
                return Forbid();

            // 2) Refresh tokens and build Graph client
            var tokens = await CheckMsTokensAsync(userId);
            var graphClient = new GraphServiceClient(new DelegateAuthenticationProvider(req =>
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
                return Task.CompletedTask;
            }));

            // 3) Page through all your Graph subscriptions and delete each one
            var deleted = new List<string>();
            var page = await graphClient.Subscriptions.Request().GetAsync();
            while (page != null)
            {
                foreach (var sub in page.CurrentPage)
                {
                    // only delete the ones you own (optional filter by NotificationUrl or Resource)
                    await graphClient.Subscriptions[sub.Id]
                                     .Request()
                                     .DeleteAsync();
                    deleted.Add(sub.Id);
                }
                page = page.NextPageRequest != null
                    ? await page.NextPageRequest.GetAsync()
                    : null;
            }

            // 4) Remove them locally as well
            var localSubs = await _context.IntegrationSubscriptions
                .Where(s => s.Provider == "Outlook" && s.UserId.ToString() == userId)
                .ToListAsync();
            _context.IntegrationSubscriptions.RemoveRange(localSubs);
            await _context.SaveChangesAsync();

            // 5) Return what you deleted
            return Ok(new
            {
                DeletedGraphSubscriptionIds = deleted,
                DeletedLocalCount = localSubs.Count
            });
        }


    }
}
