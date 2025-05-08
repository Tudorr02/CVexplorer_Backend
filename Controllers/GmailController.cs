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

namespace CVexplorer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GmailController(IConfiguration _config, UserManager<User> _userManager) : Controller
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
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetLabels()
        {
            var jwtUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? throw new Exception("JWT fără NameIdentifier");

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
        [Authorize]
        public async Task<IActionResult> Session()
        {
            var result = await HttpContext.AuthenticateAsync(
                            CookieAuthenticationDefaults.AuthenticationScheme);
            if (!result.Succeeded || !result.Principal.Identity.IsAuthenticated)
                return Unauthorized();


            // 2️⃣ Grab your “UserId” out of the Items dictionary
            if (!result.Properties.Items.TryGetValue("UserId", out var localUserId))
                return BadRequest("No UserId in state");

            var userId = _userManager.GetUserId(User);

            if (userId != localUserId)
                return Unauthorized();

            return Ok();
        }

    }
}
