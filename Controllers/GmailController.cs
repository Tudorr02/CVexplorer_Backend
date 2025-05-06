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

namespace CVexplorer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GmailController : Controller
    {

        private readonly IConfiguration _config;
        private readonly string[] _scopes = new[]
        {
            GmailService.Scope.GmailLabels,
            GmailService.Scope.GmailReadonly
        };

        public GmailController(IConfiguration config)
        {
            _config = config;
        }


        /// <summary>
        /// Pasul 1: Pornește OAuth2-ul Google
        /// Browserul va fi redirecționat pe Google, iar după login+consent
        /// middleware-ul Google va prelua callback-ul pe /signin-google
        /// și va salva token-urile în cookie.
        /// </summary>

        [HttpGet("login")]
        [AllowAnonymous]
        public IActionResult Login()
        {
            
            

            var props = new AuthenticationProperties
            {
                // după autentificare+callback, mută utilizatorul aici
                RedirectUri = "/api/gmail/labels"
            };
            return Challenge(props, GoogleDefaults.AuthenticationScheme);
        }

        /// <summary>
        /// Pasul 2: După ce Google middleware a salvat token-urile în cookie,
        /// tu poți citi etichetele Gmail ale utilizatorului.
        /// </summary>
        [HttpGet("labels")]
      //  [Authorize] // e nevoie să fie autentificat prin cookie
        public async Task<IActionResult> GetLabels()
        {
            // 1. Preia token-urile din cookie
            var authResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!authResult.Succeeded)
                return Unauthorized();

            var accessToken = authResult.Properties.GetTokenValue("access_token");
            var refreshToken = authResult.Properties.GetTokenValue("refresh_token");

            // 2. Construiește credentialele Google
            var secrets = new ClientSecrets
            {
                ClientId = _config["Google:ClientId"],
                ClientSecret = _config["Google:ClientSecret"]
            };
            var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = secrets,
                Scopes = _scopes
            });

            var tokenResponse = new TokenResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken
            };

            var credential = new UserCredential(flow, authResult.Principal.FindFirst(c => c.Type == ClaimTypes.NameIdentifier)?.Value ?? "user", tokenResponse);

            // 3. Apelează Gmail API
            var gmailService = new GmailService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "CVexplorerWebClient"
            });

            var labelRequest = gmailService.Users.Labels.List("me");
            var labelResponse = await labelRequest.ExecuteAsync();

            return Ok(labelResponse.Labels);
        }

    }
}
