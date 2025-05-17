using Google.Apis.Auth.AspNetCore3;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Google;
using CVexplorer.Models.Domain;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using CVexplorer.Data;
using Google.Apis.Gmail.v1.Data;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CVexplorer.Services.Interface;
using CVexplorer.Models.DTO;
using CVexplorer.Repositories.Interface;

namespace CVexplorer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GmailController(IConfiguration _config, ICVRepository _cvRepository,UserManager<User> _userManager, DataContext _context , IBackgroundTaskQueue _queue , ILogger<GmailController> _logger , IRoundRepository _roundRepository) : Controller
    {
        private readonly string[] _scopes = new[]
        {
            GmailService.Scope.GmailLabels,
            GmailService.Scope.GmailReadonly
        };

        [NonAction]
        public async Task<UserCredential> CheckTokensAsync(string userId)
        {
            // 1️⃣ Încarcă user-ul
            var user = await _userManager.FindByIdAsync(userId)
                       ?? throw new Exception("Utilizator inexistent");

            // 2️⃣ Citește token-urile din AspNetUserTokens
            var accessToken = await _userManager.GetAuthenticationTokenAsync(
                                   user, GoogleDefaults.AuthenticationScheme, "access_token");
            var refreshToken = await _userManager.GetAuthenticationTokenAsync(
                                   user, GoogleDefaults.AuthenticationScheme, "refresh_token");
            var expiresAtStr = await _userManager.GetAuthenticationTokenAsync(
                                   user, GoogleDefaults.AuthenticationScheme, "expires_at");

            // 3️⃣ Parsează expires_at din secunde UNIX
            DateTimeOffset? expiresAt = null;
            if (long.TryParse(expiresAtStr, out var unix))
                expiresAt = DateTimeOffset.FromUnixTimeSeconds(unix);

            // 4️⃣ Configurează flow-ul Google
            var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets
                {
                    ClientId = _config["Google:ClientId"],
                    ClientSecret = _config["Google:ClientSecret"]
                },
                Scopes = _scopes
            });

            // 5️⃣ Creează credential-ul cu token-urile existente
            var tokenResponse = new TokenResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresInSeconds = expiresAt.HasValue
                    ? (long?)(expiresAt.Value - DateTimeOffset.UtcNow).TotalSeconds
                    : null
            };
            var credential = new UserCredential(flow, userId, tokenResponse);

            // 6️⃣ Dacă accesul e expirat (sau nu aveai expiresAt), reîmprospătează
            if (!expiresAt.HasValue || expiresAt.Value <= DateTimeOffset.UtcNow)
            {
                var gotNew = await credential.RefreshTokenAsync(CancellationToken.None);
                if (!gotNew)
                    throw new Exception("Nu am putut reîmprospăta token-ul Google.");

                // 7️⃣ Salvează în DB noile token-uri
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

        [HttpGet("login")]
        [Authorize]
        public IActionResult Login()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();

            var props = new AuthenticationProperties();
            props.Items["UserId"] = userId;
            props.RedirectUri = _config["Google:RedirectUri"];
            return Challenge(props, GoogleDefaults.AuthenticationScheme);
        }


        [HttpGet("labels")]
        [Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{"GoogleCookie"}")]
        public async Task<IActionResult> GetLabels(string publicPosId)
        {
            
            var jwtResult = await HttpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
            //var cookieResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            var cookieResult = await HttpContext.AuthenticateAsync("GoogleCookie");

            if (!jwtResult.Succeeded || !cookieResult.Succeeded)
                return Forbid(); 

            var jwtUserId = jwtResult.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var cookieUserId = cookieResult.Properties.Items["UserId"];

            if (jwtUserId == null || cookieUserId == null || jwtUserId != cookieUserId)
                return Forbid();

            
            var credential = await CheckTokensAsync(jwtUserId);

            // 3. Apelează Gmail API
            var gmailService = new GmailService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "CVexplorerWebClient"
            });
            var labels = await gmailService.Users.Labels.List("me").ExecuteAsync();

            var position = _context.Positions.First(p => p.PublicId == publicPosId);
            var existingSubs = await _context.IntegrationSubscriptions
       .Where(s => s.Provider == "Gmail" && s.UserId.ToString() == jwtUserId && s.PositionId == position.Id)
       .ToListAsync();
            var subscribedIds = existingSubs
       .Select(s => s.LabelId)
       .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var result = labels.Labels.Select(lbl => new
            {
                Id = lbl.Id,
                Name = lbl.Name,
                Selected = subscribedIds.Contains(lbl.Id)
            });
            //return Ok(labels.Labels);

            return Ok(result);

        }

        [HttpGet("session")]
        [Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{"GoogleCookie"}")]

        public async Task<IActionResult> Session()
        {
            var jwtResult = await HttpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
            //var cookieResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            var cookieResult = await HttpContext.AuthenticateAsync("GoogleCookie");

            if (!jwtResult.Succeeded || !cookieResult.Succeeded)
                return Forbid();

            var jwtUserId = jwtResult.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var cookieUserId = cookieResult.Properties.Items["UserId"];

            if (jwtUserId == null || cookieUserId == null || jwtUserId != cookieUserId)
                return Forbid();


            return Ok();
        }


        [HttpPost("watch")]
        [Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{"GoogleCookie"}")]

        public async Task<IActionResult> WatchGmail(List<string> labelIds, string positionPublicId)
        {
            var jwtResult = await HttpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
            //var cookieResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            var cookieResult = await HttpContext.AuthenticateAsync("GoogleCookie");

            if (!jwtResult.Succeeded || !cookieResult.Succeeded)
                return Forbid();

            var jwtUserId = jwtResult.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var cookieUserId = cookieResult.Properties.Items["UserId"];

            if (jwtUserId == null || cookieUserId == null || jwtUserId != cookieUserId)
                return Forbid();

            var position = await _context.Positions
                .SingleOrDefaultAsync(p => p.PublicId == positionPublicId)
                ?? throw new Exception("Poziție inexistentă");

            var userId = jwtUserId;

            var user = await _userManager.FindByIdAsync(userId)
                       ?? throw new Exception("Utilizator inexistent");

            // 2. Inițializează GmailService
            var cred = await CheckTokensAsync(userId);
            var gmailSvc = new GmailService(new BaseClientService.Initializer
            {
                HttpClientInitializer = cred,
                ApplicationName = "CVexplorerWebClient"
            });

            

            // 4. Preluăm etichetele deja folosite pentru acest user și poziție
            var existingSubs = await _context.IntegrationSubscriptions
                .Where(s => s.UserId == user.Id && s.Provider == "Gmail" && s.PositionId == position.Id)
                .ToListAsync();

            

            // 5. Dacă nu există niciun subscription, creăm o rundă
            Round initialRound = null;
            if (!existingSubs.Any())
            {
                initialRound = await _roundRepository.CreateAsync(position.Id);
            }

            // 6. Identificăm etichetele de adăugat și de eliminat
            var toAdd = labelIds.Distinct().Except(existingSubs.Select(s => s.LabelId));
            var toRemove = existingSubs.Select(s => s.LabelId).Except(labelIds.Distinct());

            // 7. Pentru etichetele eliminate, apelăm stop și ștergem din DB
            if (toRemove.Any())
            {
                // Oprire watch complet (Gmail API nu suportă unwatch per label)
                await gmailSvc.Users.Stop("me").ExecuteAsync();

                // Ștergem subscripțiile locale
                var subsToDelete = existingSubs.Where(s => toRemove.Contains(s.LabelId)).ToList();
                _context.IntegrationSubscriptions.RemoveRange(subsToDelete);


            }

            // Combinăm cu noile etichete și eliminăm duplicatele
            var remainingLabels = existingSubs
                .Where(s => !toRemove.Contains(s.LabelId))
                .Select(s => s.LabelId)
                .Union(toAdd)
                .Distinct()
                .ToArray();

            if (!remainingLabels.Any())
            {
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    RequestedLabels = labelIds.Distinct()
                });
            }

            // 3. Apelează Users.Watch
            var watchReq = new WatchRequest
            {
                LabelIds = remainingLabels,
                TopicName = $"projects/{_config["Google:ProjectId"]}/topics/{_config["Google:GmailTopic"]}"
               
            };
            var watchResp = await gmailSvc.Users.Watch(watchReq, "me").ExecuteAsync();
            var profile = await gmailSvc.Users.GetProfile("me").ExecuteAsync();

            // 6. Pentru fiecare etichetă cerută creăm sau actualizăm IntegrationSubscription
            foreach (var lbl in labelIds.Distinct())
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
                        RoundId = initialRound != null ? initialRound.Id : existingSubs.First().RoundId
                    };
                    _context.IntegrationSubscriptions.Add(sub);
                }

                // setăm token-ul și expirarea
                sub.SyncToken = watchResp.HistoryId.ToString();
                sub.ExpiresAt = DateTimeOffset.UtcNow.AddMilliseconds((double)watchResp.Expiration);
                sub.UpdatedAt = DateTimeOffset.UtcNow;
            }

            // 7. Salvăm toate modificările odată
            await _context.SaveChangesAsync();

            // 8. Răspuns
            return Ok(new
            {
                RequestedLabels = labelIds.Distinct()
            });
        }

        [HttpPost("unwatch")]
        [Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{"GoogleCookie"}")]

        public async Task<IActionResult> UnwatchGmail(string labelId, string positionPublicId)
        {
            // 1) Autentificare duală
            var jwt = await HttpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
            var ck = await HttpContext.AuthenticateAsync("GoogleCookie");
            if (!jwt.Succeeded || !ck.Succeeded) return Forbid();

            var userId = jwt.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var cookieUserId = ck.Properties.Items["UserId"];
            if (userId == null || cookieUserId == null || userId != cookieUserId)
                return Forbid();

            // 2) Găsește poziția și user-ul
            var position = await _context.Positions
                .SingleOrDefaultAsync(p => p.PublicId == positionPublicId)
                ?? throw new Exception("Poziție inexistentă");
            var user = await _userManager.FindByIdAsync(userId)
                       ?? throw new Exception("Utilizator inexistent");

            // 3) Inițializează GmailService
            var cred = await CheckTokensAsync(userId);
            var gmailSvc = new GmailService(new BaseClientService.Initializer
            {
                HttpClientInitializer = cred,
                ApplicationName = "CVexplorerWebClient"
            });

            // 4) Oprește toate watch-urile curente pentru acest user
            await gmailSvc.Users.Stop("me").ExecuteAsync();

            // 5) Șterge din DB subscripția specifică
            var subToRemove = await _context.IntegrationSubscriptions
                .SingleOrDefaultAsync(s =>
                    s.UserId == user.Id &&
                    s.Provider == "Gmail" &&
                    s.LabelId == labelId &&
                    s.PositionId == position.Id);
            if (subToRemove != null)
                _context.IntegrationSubscriptions.Remove(subToRemove);
                await _context.SaveChangesAsync();

            // 6) Recombină lista de label-uri rămasă și re-lansează Watch (dacă mai are sens)
            var remainingLabels = await _context.IntegrationSubscriptions
                .Where(s => s.UserId == user.Id && s.Provider == "Gmail")
                .Select(s => s.LabelId)
                .Distinct()
                .ToListAsync();

            if (remainingLabels.Any())
            {
                var watchReq = new WatchRequest
                {
                    LabelIds = remainingLabels.ToArray(),
                    TopicName = $"projects/{_config["Google:ProjectId"]}/topics/{_config["Google:GmailTopic"]}"

                };
                var watchResp = await gmailSvc.Users.Watch(watchReq, "me").ExecuteAsync();

                // actualizează SyncToken pentru toate rămase
                var now = DateTimeOffset.UtcNow;
                foreach (var sub in _context.IntegrationSubscriptions
                             .Where(s => s.UserId == user.Id && s.Provider == "Gmail"))
                {
                    sub.SyncToken = watchResp.HistoryId.ToString();
                    sub.ExpiresAt = now.AddMilliseconds((double)watchResp.Expiration);
                    sub.UpdatedAt = now;
                }

                // 7) Persistă modificările
                await _context.SaveChangesAsync();
            }

            

            return Ok(new
            {
                removedLabel = labelId,
                remainingLabels
            });
        }


        [HttpPost("push")]
        [AllowAnonymous]
        public async Task<IActionResult> GmailPush([FromBody] GmailPushDTO envelope)
        {
            // 1️⃣ decode & parse
            var raw = envelope.Message.Data;
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(raw));
            var notif = JsonSerializer.Deserialize<GmailPushNotificationDTO>(json)
                           ?? throw new Exception("Invalid push payload");
            var email = notif.EmailAddress;
            //var newHist = long.Parse(notif.HistoryId);
            var newHist = notif.HistoryId;
            _logger.LogInformation("Enqueue GmailPush job for {Email} from history {HistoryId}",
                                   email, newHist);

            // 2️⃣ determină de unde să pornească sync-ul:
            var subs = _context.IntegrationSubscriptions
                       .Where(s => s.Provider == "Gmail" && s.Email == email)
                       .ToList();

            if (subs == null)
            {
                _logger.LogWarning("No subscription found for {Email}, ignoring push", email);
                return Ok();  // ack anyway
            }

            foreach (var s in subs)
            {
      
                // enquează job cu Subscription’s Id
               await  _queue.EnqueueAsync(new PushJobDTO{
                   Provider = "Gmail",
                   SubscriptionId = s.Id.ToString(),
                   ResourceId = s.LabelId,
               });

                
            }


            return Accepted();  // 202 == OK for Pub/Sub ack
        }


        [NonAction]
        // 4️⃣ metoda pe care o va apela BackgroundService:
        public async Task ProcessHistoryAsync(long subscriptionId, CancellationToken ct)
        {
            // atenție: aici faci toată logica de „delta sync”:

           
            var sub = await _context.IntegrationSubscriptions.Include(s => s.User).Include(s => s.Round)
                             .SingleAsync(s => s.Id == subscriptionId, ct);

            var cred = await CheckTokensAsync(sub.UserId.ToString());
            var gmail = new GmailService(new BaseClientService.Initializer
            {
                HttpClientInitializer = cred,
                ApplicationName = "CVexplorerWebClient"

            });

            // 1. History.list
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

            foreach (var msgId in messageIds)
            {
                var msgReq = gmail.Users.Messages.Get("me", msgId);
                msgReq.Format = UsersResource.MessagesResource.GetRequest.FormatEnum.Full;
                var fullMsg = await msgReq.ExecuteAsync(ct);

                // extract headers once
                var hdrs = fullMsg.Payload.Headers;
                var from = hdrs.FirstOrDefault(h => h.Name == "From")?.Value ?? "<unknown>";
                var subject = hdrs.FirstOrDefault(h => h.Name == "Subject")?.Value ?? "<no subject>";

                // 5. Descărcăm toate PDF-urile (filtrare + paralelizare internă)
                var pdfFiles = await GetPdfFormFilesAsync(gmail, "me", msgId, fullMsg.Payload.Parts ?? Enumerable.Empty<MessagePart>(), ct);

                if (!pdfFiles.Any())
                {
                    _logger.LogWarning("From: {From}, Subject: {Subject} — nu conține PDF", from, subject);
                    continue;
                }

                _logger.LogError("From: {From}, Subject: {Subject} — CONȚINE PDF", from, subject);

                // 6. Obținem publicId-ul poziției o singură dată
                var positionPublicId = await _context.Positions
                    .Where(p => p.Id == sub.PositionId)
                    .Select(p => p.PublicId)
                    .FirstOrDefaultAsync(ct);

                // 7. Upload
                foreach (var file in pdfFiles)
                {
                    await _cvRepository.UploadDocumentAsync(file, positionPublicId, sub.User.Id, sub.RoundId);
                }
            }



            sub.SyncToken = history.HistoryId.ToString();
            sub.UpdatedAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync(ct);


        }
        [NonAction]
        public async Task<List<IFormFile>> GetPdfFormFilesAsync(GmailService gmailSvc, string userId, string messageId, IEnumerable<MessagePart> parts, CancellationToken ct = default)
        {
            // 1. Filtrăm doar părțile care sunt PDF
            var pdfParts = parts
                .Where(p => !string.IsNullOrEmpty(p.Filename)
                            && (p.Filename.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                                || p.MimeType == "application/pdf"))
                .ToList();

            // 2. Pentru fiecare attachment, pornim un task de descărcare
            var downloadTasks = pdfParts.Select(async part =>
            {
                var attach = await gmailSvc.Users.Messages.Attachments
                    .Get(userId, messageId, part.Body.AttachmentId)
                    .ExecuteAsync(ct);

                // 3. Decodăm base64url
                var base64 = attach.Data;
                var bytes = Convert.FromBase64String(
                    base64.Replace('-', '+').Replace('_', '/'));

                // 4. Creăm MemoryStream și IFormFile
                var ms = new MemoryStream(bytes);
                return (IFormFile)new FormFile(ms, 0, ms.Length,
                                               name: "file",
                                               fileName: part.Filename)
                {
                    Headers = new HeaderDictionary(),
                    ContentType = part.MimeType
                };
            });

            // 5. Așteptăm ca toate descărcările să se finalizeze
            return (await Task.WhenAll(downloadTasks)).ToList();
        }

    }
}
