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
    public class GmailController(IConfiguration _config, UserManager<User> _userManager, DataContext _context , IBackgroundTaskQueue _queue , ILogger<GmailController> _logger , IRoundRepository _roundRepository) : Controller
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
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme + "," + CookieAuthenticationDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetLabels()
        {
            
            var jwtResult = await HttpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
            var cookieResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

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
            return Ok(labels.Labels);

        }

        [HttpGet("session")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme + "," + CookieAuthenticationDefaults.AuthenticationScheme)]
        public async Task<IActionResult> Session()
        {
            var jwtResult = await HttpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
            var cookieResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (!jwtResult.Succeeded || !cookieResult.Succeeded)
                return Forbid();

            var jwtUserId = jwtResult.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var cookieUserId = cookieResult.Properties.Items["UserId"];

            if (jwtUserId == null || cookieUserId == null || jwtUserId != cookieUserId)
                return Forbid();


            return Ok();
        }


        [HttpPost("watch")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme + "," + CookieAuthenticationDefaults.AuthenticationScheme)]
        public async Task<IActionResult> WatchGmail(string labelId, string positionPublicId)
        {
            var jwtResult = await HttpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
            var cookieResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

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


            var allLabels = await _context.IntegrationSubscriptions
            .Where(s => s.UserId == user.Id && s.Provider == "Gmail")
            .Select(s => s.LabelId)
            .ToListAsync();


            if (!allLabels.Contains(labelId))
            {
                allLabels.Add(labelId);
            }

            // 3. Apelează Users.Watch
            var watchReq = new WatchRequest
            {
                LabelIds = allLabels.Distinct().ToArray(),
                TopicName = $"projects/{_config["Google:ProjectId"]}/topics/{_config["Google:GmailTopic"]}"
               
            };
            var watchResp = await gmailSvc.Users.Watch(watchReq, "me").ExecuteAsync();
            var profile = await gmailSvc.Users.GetProfile("me").ExecuteAsync();


            var sub = await _context.IntegrationSubscriptions
                 .SingleOrDefaultAsync(s =>
                     s.Provider == "Gmail" &&
                     s.UserId == user.Id &&
                     s.LabelId == labelId &&
                     s.PositionId == position.Id &&
                     s.Email == profile.EmailAddress
                 );
            if (sub == null)
            {
                var round = await _roundRepository.CreateAsync(position.Id);
                sub = new IntegrationSubscription
                {
                    UserId = user.Id,
                    Provider = "Gmail",
                    LabelId = labelId,
                    PositionId = position.Id,
                    Email = profile.EmailAddress,
                    SubscriptionName = watchReq.TopicName,
                    RoundId = round.Id
                };
                _context.IntegrationSubscriptions.Add(sub);
            }
            sub.SyncToken = watchResp.HistoryId.ToString();
            sub.ExpiresAt = DateTimeOffset.UtcNow.AddMilliseconds((double)watchResp.Expiration);
            sub.UpdatedAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                watchReq.LabelIds,
                watchResp.HistoryId,
                watchResp.Expiration
            });
        }

        [HttpPost("unwatch")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme + "," +
                            CookieAuthenticationDefaults.AuthenticationScheme)]
        public async Task<IActionResult> UnwatchGmail(string labelId, string positionPublicId)
        {
            // 1) Autentificare duală
            var jwt = await HttpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
            var ck = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
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
               await  _queue.EnqueueAsync(new GmailPushJobDTO{
                    InterogationSubscriptionId = s.Id,
                    EmailAddress = email
                });

                
            }


            return Accepted();  // 202 == OK for Pub/Sub ack
        }


        
        
    }
}
