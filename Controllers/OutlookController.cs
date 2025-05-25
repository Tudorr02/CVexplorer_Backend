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
using System.Text.RegularExpressions;
using Microsoft.Identity.Client.Platforms.Features.DesktopOs.Kerberos;

namespace CVexplorer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OutlookController(IOutlookService _oService, IBackgroundTaskQueue _queue, ICVRepository _cvRepository, ILogger<OutlookController> _logger, UserManager<Models.Domain.User> _userManager, IRoundRepository _roundRepository, IConfiguration _config, DataContext _context) : Controller
    {


        [HttpGet("Connect")]
        [Authorize]
        public IActionResult Connect()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();


            var props = new AuthenticationProperties();
            props.Items["UserId"] = userId;
            props.RedirectUri = _config["Microsoft:RedirectUri"];

            return Challenge(props, "Microsoft");

        }



        [HttpGet("Session")]
        [Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{"MicrosoftCookie"}")]

        public async Task<IActionResult> Session()
        {
            
            var jwtResult = await HttpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
            var cookieResult = await HttpContext.AuthenticateAsync("MicrosoftCookie");
            var sessionActive = false;
            if (!jwtResult.Succeeded || !cookieResult.Succeeded)
                return Ok(new { sessionActive });

            var jwtUserId = jwtResult.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var cookieUserId = cookieResult.Properties.Items["UserId"];

            if (jwtUserId == null || cookieUserId == null || jwtUserId != cookieUserId)
                return Ok(new { sessionActive });

            var tokens = await _oService.GetOrRefreshTokensAsync(jwtUserId);
            if (tokens == null)
                return Ok(new { sessionActive });

            sessionActive= true;
            return Ok(new { sessionActive });
        }


        [HttpGet("Folders")]
        [Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{"MicrosoftCookie"}")]
        public async Task<ActionResult<List<OutlookFolderListDTO>>> GetFolders(string publicPosId)
        {
            var jwtResult = await HttpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
            var cookieResult = await HttpContext.AuthenticateAsync("MicrosoftCookie");

            if (!jwtResult.Succeeded || !cookieResult.Succeeded)
                return Forbid();

            var jwtUserId = jwtResult.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var cookieUserId = cookieResult.Properties.Items["UserId"];

            if (jwtUserId == null || cookieUserId == null || jwtUserId != cookieUserId)
                return Forbid();

            // 1) Obținem token-urile (va face refresh dacă e nevoie)
            var tokens = await _oService.GetOrRefreshTokensAsync(jwtUserId);

            if (tokens == null)
                return BadRequest("Could not retrieve tokens");

            return await _oService.GetFoldersAsync(jwtUserId, tokens, publicPosId);

        }

        [HttpPost("Watch")]
        [Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{"MicrosoftCookie"}")]
        public async Task<ActionResult<List<OutlookFolderListDTO>>> SubscribeFolders(List<string> folderIds, string publicPosId)
        {

            var jwtResult = await HttpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
            var cookieResult = await HttpContext.AuthenticateAsync("MicrosoftCookie");
            if (!jwtResult.Succeeded || !cookieResult.Succeeded)
                return Forbid();

            var userId = jwtResult.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var cookieId = cookieResult.Properties.Items["UserId"];
            if (userId == null || cookieId == null || userId != cookieId)
                return Forbid();

            var tokens = await _oService.GetOrRefreshTokensAsync(userId);

            if (tokens == null)
                return BadRequest("Could not retrieve tokens");

            try
            {
                return await _oService.SubscribeFolders(folderIds, userId, tokens, publicPosId);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }

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

        [HttpPost("Push")]
        [AllowAnonymous]
        public async Task<IActionResult> Notifications()
        {


            if (Request.Query.TryGetValue("validationToken", out var validationTokens))
            {
                var token = validationTokens.FirstOrDefault();
                if (!string.IsNullOrEmpty(token))
                    return Content(token, "text/plain");
            }


            string json;
            using (var reader = new StreamReader(Request.Body))
            {
                json = await reader.ReadToEndAsync();
            }


            NotificationCollection notifications;
            try
            {
                notifications = JsonSerializer.Deserialize<NotificationCollection>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? throw new Exception("Invalid payload");
            }
            catch (Exception ex)
            {

                _logger.LogWarning("Unable to parse notification payload {Msg}", ex.Message);
                return Accepted();
            }

            Accepted();


            foreach (var note in notifications.Value)
            {

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
                    SubscriptionId = sub.Id.ToString(),
                    ResourceId = sub.LabelId,
                    MessageId = note.ResourceData.Id
                };

                await _queue.EnqueueAsync(job);
            }


            return StatusCode(StatusCodes.Status202Accepted);
        }


        [HttpPost("Unsubscribe")]
        [Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},MicrosoftCookie")]
        public async Task<IActionResult> ClearSubscriptions(string publicPosId)
        {

            var jwtResult = await HttpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
            var cookieResult = await HttpContext.AuthenticateAsync("MicrosoftCookie");
            if (!jwtResult.Succeeded || !cookieResult.Succeeded)
                return Forbid();

            var userId = jwtResult.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var cookieId = cookieResult.Properties.Items["UserId"];
            if (userId == null || cookieId == null || userId != cookieId)
                return Forbid();

            var tokens = await _oService.GetOrRefreshTokensAsync(userId);

            if (tokens == null)
                return BadRequest("Could not retrieve tokens");


            try
            {
                var result = await _oService.UnsubscribeAsync(userId, tokens, publicPosId);
                if (result)
                    return Ok();
                else
                    return StatusCode(500, "Internal Server Error");

            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }

        }

        [HttpPost("Disconnect")]
        [Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},MicrosoftCookie")]

        public async Task<IActionResult> Disconnect()
        {
            var jwtResult = await HttpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
            var cookieResult = await HttpContext.AuthenticateAsync("MicrosoftCookie");
            if (!jwtResult.Succeeded || !cookieResult.Succeeded)
                return Forbid();

            var userId = jwtResult.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var cookieId = cookieResult.Properties.Items["UserId"];
            if (userId == null || cookieId == null || userId != cookieId)
                return Forbid();

            var tokens = await _oService.GetOrRefreshTokensAsync(userId);

            if (tokens == null)
                return BadRequest("Could not retrieve tokens");

            await _oService.Disconnect ( userId, tokens);

            // 🔁 Șterge tokenurile Gmail salvate în Identity
            Response.Cookies.Delete("Microsoft.Auth", new CookieOptions
            {
                Path = "/",
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None
            });

            return Ok(new { message = "Outlook account disconnected." });


        }

        [HttpGet("Subs")]
        public async Task<List<Subscription>> GetAllGraphSubscriptionsAsync()
        {

            var tokens = await _oService.GetOrRefreshTokensAsync("11");
            // 1) Build a Graph client using your existing tokens
            var graphClient = new GraphServiceClient(new DelegateAuthenticationProvider(msg =>
            {
                msg.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
                return Task.CompletedTask;
            }));

            var allSubs = new List<Subscription>();

            // 2) Kick off the first page request
            var page = await graphClient.Subscriptions
                                        .Request()
                                        .GetAsync();

            // 3) Accumulate pages
            allSubs.AddRange(page.CurrentPage);
            while (page.NextPageRequest != null)
            {
                page = await page.NextPageRequest.GetAsync();
                allSubs.AddRange(page.CurrentPage);
            }

            return allSubs;
        }
    }
}
