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

namespace CVexplorer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GmailController(IConfiguration _config, UserManager<User> _userManager, DataContext _context) : Controller
    {
        private readonly string[] _scopes = new[]
        {
            GmailService.Scope.GmailLabels,
            GmailService.Scope.GmailReadonly
        };

        private async Task<UserCredential> CheckTokensAsync(string userId)
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
            props.RedirectUri = "/api/gmail/labels";

            
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
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme + "," +
                                 CookieAuthenticationDefaults.AuthenticationScheme)]
        public async Task<IActionResult> WatchGmail(string labelId)
        {
            var jwtResult = await HttpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
            var cookieResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (!jwtResult.Succeeded || !cookieResult.Succeeded)
                return Forbid();

            var jwtUserId = jwtResult.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var cookieUserId = cookieResult.Properties.Items["UserId"];

            if (jwtUserId == null || cookieUserId == null || jwtUserId != cookieUserId)
                return Forbid();

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

            // 3. Apelează Users.Watch
            var watchReq = new WatchRequest
            {
                LabelIds = new[] { labelId },
                TopicName = $"projects/{_config["Google:ProjectId"]}/topics/{_config["Google:GmailTopic"]}"
            };
            var watchResp = await gmailSvc.Users.Watch(watchReq, "me").ExecuteAsync();
            var profile = await gmailSvc.Users.GetProfile("me").ExecuteAsync();


            var sub = await _context.IntegrationSubscriptions
                .SingleOrDefaultAsync(s => s.Provider == "Gmail" && s.UserId == user.Id && s.Resource == $"me/label/{labelId}" && s.Email == profile.EmailAddress);

            if (sub == null)
            {
                sub = new IntegrationSubscription
                {
                    UserId = user.Id,
                    Provider = "Gmail",
                    Resource = $"me/label/{labelId}",
                    Email = profile.EmailAddress,
                };
                _context.IntegrationSubscriptions.Add(sub);
            }
            sub.SyncToken = watchResp.HistoryId.ToString();
            sub.ExpiresAt = DateTimeOffset.UtcNow.AddMilliseconds((double)watchResp.Expiration);
            sub.UpdatedAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                watchResp.HistoryId,
                watchResp.Expiration
            });
        }


        [HttpPost("push")]
        [AllowAnonymous]
        public async Task<IActionResult> GmailPush([FromBody] PubsubPushMessage envelope)
        {
            // 1) Decodează baza64
            var msg = envelope.Message;
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(msg.Data));
            var notif = JsonSerializer.Deserialize<PushNotificationDto>(json)
                        ?? throw new Exception("Invalid push payload");

            // 2) Găsește subscripția după email
            var sub = await _context.IntegrationSubscriptions
                .SingleAsync(s =>
                    s.Provider == "Gmail" &&
                    s.Email == notif.EmailAddress
                );

            // 3) Reconstruiește user-ul
            var userId = sub.UserId.ToString();
            var cred = await CheckTokensAsync(userId);
            var gmailSvc = new GmailService(new BaseClientService.Initializer
            {
                HttpClientInitializer = cred,
                ApplicationName = "CVexplorerWebClient"
            });

            // 4) Delta‐sync history
            var histReq = gmailSvc.Users.History.List("me");
            histReq.StartHistoryId = (ulong?)(long.Parse(sub.SyncToken) + 1);
            var history = await histReq.ExecuteAsync();

            // 5) Procesează mesajele noi (opțional)
            // …

            // 6) Actualizează SyncToken
            sub.SyncToken = notif.HistoryId;
            sub.UpdatedAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync();

            return Ok();
        }

        // DTO‐urile extinse:
        public class PubsubPushMessage
        {
            [JsonPropertyName("message")]
            public PubsubMessage Message { get; set; }

            [JsonPropertyName("subscription")]
            public string Subscription { get; set; }
        }

        public class PubsubMessage
        {
            [JsonPropertyName("data")]
            public string Data { get; set; }

            //[JsonPropertyName("attributes")]
            //public IDictionary<string, string> Attributes { get; set; }

            // câmpurile camelCase pe care le foloseai deja
            [JsonPropertyName("messageId")]
            public string MessageId { get; set; }

            [JsonPropertyName("publishTime")]
            public string PublishTime { get; set; }

            // PLUS cele cu underscore din JSON
            [JsonPropertyName("message_id")]
            public string message_id { get; set; }

            [JsonPropertyName("publish_time")]
            public string publish_time { get; set; }
        }
        public class PushNotificationDto
        {
            [JsonPropertyName("emailAddress")]
            public string EmailAddress { get; set; }

            [JsonPropertyName("historyId")]
            public string HistoryId { get; set; }
        }
    }
}
