using CVexplorer.Models.DTO;
using CVexplorer.Services.Interface;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Gmail.v1.Data;
using CVexplorer.Data;
using Microsoft.EntityFrameworkCore;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using CVexplorer.Models.Domain;
using CVexplorer.Repositories.Interface;
using Google.Apis.Gmail.v1;
using CVexplorer.Repositories.Implementation;
using CVexplorer.Controllers;
using Azure;
using System;

namespace CVexplorer.Services.Implementation
{
    public class GmailService(ICVRepository _cvRepository,IRoundRepository _roundRepository,UserManager<User> _userManager, IConfiguration _config, DataContext _context, ILogger<GmailService> _logger) : IGmailService
    {
        public async Task<List<GmailFolderListDTO>> GetLabelsAsync(UserCredential? credential, string publicPositionId, string userId)
        {
            var gmailService = new Google.Apis.Gmail.v1.GmailService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = _config["Google:ApplicationName"]
            });
            var labels = await gmailService.Users.Labels.List("me").ExecuteAsync();

            var position = _context.Positions.First(p => p.PublicId == publicPositionId);
            var existingSubs = await _context.IntegrationSubscriptions
               .Where(s => s.Provider == "Gmail" && s.UserId.ToString() == userId && s.PositionId == position.Id)
               .ToListAsync();
            var subscribedIds = existingSubs
               .Select(s => s.LabelId)
               .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var result = labels.Labels.Select(lbl => new GmailFolderListDTO
            {
                Id = lbl.Id,
                Name = lbl.Name,
                isSubscribed = subscribedIds.Contains(lbl.Id)
            }).ToList();

            return result;
        }

        public async Task<UserCredential> GetOrRefreshTokensAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId)
                       ?? throw new Exception("User does not exist");
            var accessToken = await _userManager.GetAuthenticationTokenAsync(
                                   user, GoogleDefaults.AuthenticationScheme, "access_token");
            var refreshToken = await _userManager.GetAuthenticationTokenAsync(
                                   user, GoogleDefaults.AuthenticationScheme, "refresh_token");
            var expiresAtStr = await _userManager.GetAuthenticationTokenAsync(
                                   user, GoogleDefaults.AuthenticationScheme, "expires_at");


            DateTimeOffset? expiresAt = null;
            if (long.TryParse(expiresAtStr, out var unix))
                expiresAt = DateTimeOffset.FromUnixTimeSeconds(unix);


            var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets
                {
                    ClientId = _config["Google:ClientId"],
                    ClientSecret = _config["Google:ClientSecret"]
                },
                Scopes = new[] {
                   Google.Apis.Gmail.v1.GmailService.Scope.GmailLabels,
                   Google.Apis.Gmail.v1.GmailService.Scope.GmailReadonly
                }
            });


            var tokenResponse = new TokenResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresInSeconds = expiresAt.HasValue
                    ? (long?)(expiresAt.Value - DateTimeOffset.UtcNow).TotalSeconds
                    : null
            };
            var credential = new UserCredential(flow, userId, tokenResponse);


            if (!expiresAt.HasValue || expiresAt.Value <= DateTimeOffset.UtcNow)
            {
                var gotNew = await credential.RefreshTokenAsync(CancellationToken.None);
                if (!gotNew)
                    return null;


                await _userManager.SetAuthenticationTokenAsync(
                    user, GoogleDefaults.AuthenticationScheme,
                    "access_token", credential.Token.AccessToken);

                if (!string.IsNullOrEmpty(credential.Token.RefreshToken))
                    await _userManager.SetAuthenticationTokenAsync(
                        user, GoogleDefaults.AuthenticationScheme,
                        "refresh_token", credential.Token.RefreshToken);

                var newExpiresAt = DateTimeOffset.UtcNow
                    .AddSeconds(credential.Token.ExpiresInSeconds ?? 0)
                    .ToUnixTimeSeconds()
                    .ToString();
                await _userManager.SetAuthenticationTokenAsync(
                    user, GoogleDefaults.AuthenticationScheme,
                    "expires_at", newExpiresAt);


            }

            return credential;
        }


        public async Task<List<GmailFolderListDTO>> WatchLabels(UserCredential cred,List<string> labelIds, string positionPublicId, string userId , string? roundId= null)
        {
            var position = await _context.Positions
               .SingleOrDefaultAsync(p => p.PublicId == positionPublicId)
               ?? throw new Exception("Position does not exist");

            var user = await _userManager.FindByIdAsync(userId)
                       ?? throw new Exception("User does not exist");

            
            
            
            var existingSubs = await _context.IntegrationSubscriptions
                .Where(s => s.UserId == user.Id && s.Provider == "Gmail" && s.PositionId == position.Id)
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

            var gmailSvc = new Google.Apis.Gmail.v1.GmailService(new BaseClientService.Initializer
            {
                HttpClientInitializer = cred,
                ApplicationName = _config["Google:ApplicationName"]
            });


            var distinctLabels = labelIds.Distinct().ToArray();
            var toAdd = distinctLabels.Except(existingSubs.Select(s => s.LabelId));
            var toRemove = existingSubs.Select(s => s.LabelId).Except(distinctLabels);


            if (toRemove.Count() == existingSubs.Count)
            {
                await gmailSvc.Users.Stop("me").ExecuteAsync();
                _context.IntegrationSubscriptions.RemoveRange(existingSubs);
                existingSubs.Clear();
            }
            else if (toRemove.Any())
            {
                await gmailSvc.Users.Stop("me").ExecuteAsync();
                var subsToDelete = existingSubs.Where(s => toRemove.Contains(s.LabelId)).ToList();
                _context.IntegrationSubscriptions.RemoveRange(subsToDelete);
                existingSubs.RemoveAll(s => toRemove.Contains(s.LabelId));
            }


            var remainingLabels = existingSubs
                  .Select(s => s.LabelId)
                  .Union(toAdd)
                  .Distinct()
                  .ToArray();

            if (!remainingLabels.Any())
            {
                await _context.SaveChangesAsync();

                var labels = await gmailSvc.Users.Labels.List("me").ExecuteAsync();
                return labels.Labels.Select(lbl => new GmailFolderListDTO
                {
                    Id = lbl.Id,
                    Name = lbl.Name,
                    isSubscribed = false
                }).ToList();

            }

            
            var watchReq = new WatchRequest
            {
                LabelIds = remainingLabels,
                TopicName = $"projects/{_config["Google:ProjectId"]}/topics/{_config["Google:GmailTopic"]}"

            };
            var watchResp = await gmailSvc.Users.Watch(watchReq, "me").ExecuteAsync();
            var profile = await gmailSvc.Users.GetProfile("me").ExecuteAsync();

            
            foreach (var lbl in remainingLabels)
            {

                var sub = existingSubs.FirstOrDefault(s => s.LabelId == lbl);
                if (sub == null)
                {
                    sub = new IntegrationSubscription
                    {
                        UserId = user.Id,
                        Provider = "Gmail",
                        LabelId = lbl,
                        PositionId = position.Id,
                        Email = profile.EmailAddress,
                        SubscriptionName = watchReq.TopicName,
                        RoundId = actualRound.Id
                    };
                    _context.IntegrationSubscriptions.Add(sub);
                }
                else
                {
                    sub.RoundId = actualRound.Id;
                }

                // setăm token-ul și expirarea
                sub.SyncToken = watchResp.HistoryId.ToString();
                sub.ExpiresAt = DateTimeOffset.UtcNow.AddMilliseconds((double)watchResp.Expiration);
                sub.UpdatedAt = DateTimeOffset.UtcNow;
            }

            // 7. Salvăm toate modificările odată
            await _context.SaveChangesAsync();

            var allLabels = await gmailSvc.Users.Labels.List("me").ExecuteAsync();

            var subscribedIds = existingSubs.Select(s => s.LabelId).ToHashSet();

            var result = allLabels.Labels.Select(lbl => new GmailFolderListDTO
            {
                Id = lbl.Id,
                Name = lbl.Name,
                isSubscribed = subscribedIds.Contains(lbl.Id)
            }).ToList();

            return result;
        }

        public async Task<bool> Unsubscribe(UserCredential cred, string positionPublicId, string userId)
        {
           
            var position = await _context.Positions
               .SingleOrDefaultAsync(p => p.PublicId == positionPublicId)
               ?? throw new Exception("Position does not exist");

            var user = await _userManager.FindByIdAsync(userId)
                       ?? throw new Exception("User does not exist");

            var gmailSvc = new Google.Apis.Gmail.v1.GmailService(new BaseClientService.Initializer
            {
                HttpClientInitializer = cred,
                ApplicationName = _config["Google:ApplicationName"]
            });

            // Oprire watch complet (Gmail API nu suportă unwatch per label)
            await gmailSvc.Users.Stop("me").ExecuteAsync();

            var existingSubs = await _context.IntegrationSubscriptions
                .Where(s => s.UserId == user.Id && s.Provider == "Gmail" && s.PositionId == position.Id)
                .ToListAsync();

            
            _context.IntegrationSubscriptions.RemoveRange(existingSubs);

            await _context.SaveChangesAsync();

            return true;

        }

        public async Task<List<IFormFile>> GetPdfFormFilesAsync(Google.Apis.Gmail.v1.GmailService gmailSvc, string userId, string messageId, IEnumerable<MessagePart> parts, CancellationToken ct = default)
        {
            
            var pdfParts = parts
                .Where(p => !string.IsNullOrEmpty(p.Filename)
                            && (p.Filename.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                                || p.MimeType == "application/pdf"))
                .ToList();

            
            var downloadTasks = pdfParts.Select(async part =>
            {
                var attach = await gmailSvc.Users.Messages.Attachments
                    .Get(userId, messageId, part.Body.AttachmentId)
                    .ExecuteAsync(ct);

                
                var base64 = attach.Data;
                var bytes = Convert.FromBase64String(
                    base64.Replace('-', '+').Replace('_', '/'));

                
                var ms = new MemoryStream(bytes);
                return (IFormFile)new FormFile(ms, 0, ms.Length,
                                               name: "file",
                                               fileName: part.Filename)
                {
                    Headers = new HeaderDictionary(),
                    ContentType = part.MimeType
                };
            });

            
            return (await Task.WhenAll(downloadTasks)).ToList();
        }

        public async Task ProcessHistoryAsync(long subscriptionId, CancellationToken ct)
        {
           
            var sub = await _context.IntegrationSubscriptions.Include(s => s.User).Include(s => s.Round)
                             .SingleAsync(s => s.Id == subscriptionId, ct);

            var cred = await GetOrRefreshTokensAsync(sub.User.Id.ToString());

            var gmail = new Google.Apis.Gmail.v1.GmailService(new BaseClientService.Initializer
            {
                HttpClientInitializer = cred,
                ApplicationName = _config["Google:ApplicationName"]

            });

            
            var histReq = gmail.Users.History.List("me");
            histReq.StartHistoryId = ulong.Parse(sub.SyncToken) + 1;
            histReq.LabelId = sub.LabelId;
            var history = await histReq.ExecuteAsync(ct);

            var messageIds = history.History?
                .SelectMany(h =>
                    (h.MessagesAdded ?? Enumerable.Empty<HistoryMessageAdded>())
                    .Where(ma => ma.Message.LabelIds?.Contains(sub.LabelId) == true)
                    .Select(ma => ma.Message.Id)
                    .Concat(
                    (h.LabelsAdded ?? Enumerable.Empty<HistoryLabelAdded>())
                    .Where(la => la.LabelIds?.Contains(sub.LabelId) == true)
                    .Select(la => la.Message.Id)
                    )
                )
                .Distinct()
                .ToList()
              ?? new List<string>();

            int processedCVs = 0;

            foreach (var msgId in messageIds)
            {
                var msgReq = gmail.Users.Messages.Get("me", msgId);
                msgReq.Format = UsersResource.MessagesResource.GetRequest.FormatEnum.Full;
                var fullMsg = await msgReq.ExecuteAsync(ct);

                var hdrs = fullMsg.Payload.Headers;
                var from = hdrs.FirstOrDefault(h => h.Name == "From")?.Value ?? "<unknown>";
                var subject = hdrs.FirstOrDefault(h => h.Name == "Subject")?.Value ?? "<no subject>";
                var pdfFiles = await GetPdfFormFilesAsync(gmail, "me", msgId, fullMsg.Payload.Parts ?? Enumerable.Empty<MessagePart>(), ct);

                if (!pdfFiles.Any())
                { 
                    continue;
                }

                var positionPublicId = await _context.Positions
                    .Where(p => p.Id == sub.PositionId)
                    .Select(p => p.PublicId)
                    .FirstOrDefaultAsync(ct);

                foreach (var file in pdfFiles)
                {
                    try
                    {
                        var success = await _cvRepository.UploadDocumentAsync(file, positionPublicId, sub.User.Id, sub.RoundId, "Gmail");
                        processedCVs++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex.Message + ". UploadDocumentAsync failed to process for  {FileName}, user {UserId}, round {RoundId}.",
                        file.FileName, sub.UserId, sub.RoundId);
                    }
                }
            }


            sub.ProcessedCVs += processedCVs;
            sub.SyncToken = history.HistoryId.ToString();
            sub.UpdatedAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync(ct);
        }

        public async Task<SessionDTO> GetSessionDataAsync (string userId , string? publicId = null)
        {
            var user = await _userManager.FindByIdAsync(userId)
                       ?? throw new Exception("User does not exist");

        
            var expiresAtStr = await _userManager.GetAuthenticationTokenAsync(
                                   user, GoogleDefaults.AuthenticationScheme, "expires_at");

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
                s.UserId == user.Id && s.Provider == "Gmail" && s.PositionId == position.Id);

            

            return new SessionDTO
            {
                ProcessedCVs = existingSub?.ProcessedCVs ?? 0,
                IsProcessing = existingSub != null ? true : false,
                Expiry = expiry,
                ProcessingRoundId = existingSub?.Round.PublicId ?? null,
            };

        }
        public async Task Disconnect ( string userId)
        {
            var user = await _userManager.FindByIdAsync(userId)
                       ?? throw new Exception("User does not exist");

            await _userManager.RemoveAuthenticationTokenAsync(user, GoogleDefaults.AuthenticationScheme, "access_token");
            await _userManager.RemoveAuthenticationTokenAsync(user, GoogleDefaults.AuthenticationScheme, "refresh_token");
            await _userManager.RemoveAuthenticationTokenAsync(user, GoogleDefaults.AuthenticationScheme, "expires_at");

            var subs = await _context.IntegrationSubscriptions
                .Where(s => s.UserId == user.Id && s.Provider == "Gmail")
                .ToListAsync();

            if (subs.Any())
            {
                _context.IntegrationSubscriptions.RemoveRange(subs);
                await _context.SaveChangesAsync();
            }
        }

    }
}
