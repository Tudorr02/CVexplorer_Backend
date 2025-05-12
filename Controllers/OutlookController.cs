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

namespace CVexplorer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OutlookController(UserManager<Models.Domain.User> _userManager , IConfiguration _config) : Controller
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
    }
}
