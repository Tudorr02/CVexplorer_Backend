using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using CVexplorer.Data;
using System.Text;
using System.Text.Json;
using CVexplorer.Services.Interface;
using CVexplorer.Models.DTO;
using CVexplorer.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace CVexplorer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GmailController(IConfiguration _config, IGmailService _gService ,UserManager<Models.Domain.User> _userManager, DataContext _context , IBackgroundTaskQueue _queue ,ILogger<GmailController> _logger) : Controller
    {
       
        

        [HttpGet("Connect")]
        [Authorize]
        public IActionResult Connect()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();

            var props = new AuthenticationProperties();
            props.Items["UserId"] = userId;
            props.RedirectUri = _config["Google:RedirectUri"];
            return Challenge(props, GoogleDefaults.AuthenticationScheme);
        }

        [HttpPost("Disconnect")]
        [Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{"GoogleCookie"}")]
        public async Task<IActionResult> Disconnect()
        {

            var jwtResult = await HttpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
            var cookieResult = await HttpContext.AuthenticateAsync("GoogleCookie");

            if (!jwtResult.Succeeded || !cookieResult.Succeeded)
                return Forbid();

            var jwtUserId = jwtResult.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var cookieUserId = cookieResult.Properties.Items["UserId"];

            if (jwtUserId == null || cookieUserId == null || jwtUserId != cookieUserId)
                return Forbid();

            await _gService.Disconnect(jwtUserId);

            // 🔁 Șterge tokenurile Gmail salvate în Identity
            Response.Cookies.Delete("Google.Auth", new CookieOptions
            {
                Path = "/",
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None
            });

            return Ok(new { message = "Gmail account disconnected." });
        }


        [HttpGet("Folders")]
        [Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{"GoogleCookie"}")]
        public async Task<ActionResult<List<GmailFolderListDTO>>> GetLabels(string publicPosId)
        {
            
            var jwtResult = await HttpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
            var cookieResult = await HttpContext.AuthenticateAsync("GoogleCookie");

            if (!jwtResult.Succeeded || !cookieResult.Succeeded)
                return Forbid(); 

            var jwtUserId = jwtResult.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var cookieUserId = cookieResult.Properties.Items["UserId"];

            if (jwtUserId == null || cookieUserId == null || jwtUserId != cookieUserId)
                return Forbid();

            
            var credential = await _gService.GetOrRefreshTokensAsync(jwtUserId);

            if(credential == null)
                return Unauthorized("Tokens does not exist for current user");

            return await _gService.GetLabelsAsync(credential, publicPosId, jwtUserId);


        }

        [HttpGet("Session")]
        [Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{"GoogleCookie"}")]
        public async Task<IActionResult> Session()
        {
            var jwtResult = await HttpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
            var cookieResult = await HttpContext.AuthenticateAsync("GoogleCookie");

            if (!jwtResult.Succeeded || !cookieResult.Succeeded)
                return Forbid("GoogleCookie");

            var jwtUserId = jwtResult.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var cookieUserId = cookieResult.Properties.Items["UserId"];

            if (jwtUserId == null || cookieUserId == null || jwtUserId != cookieUserId)
                return Forbid("GoogleCookie");

            var credential = await _gService.GetOrRefreshTokensAsync(jwtUserId);

            if (credential == null)
                return Forbid("GoogleCookie");

            return Ok();
        }


        [HttpPost("Watch")]
        [Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{"GoogleCookie"}")]

        public async Task<ActionResult<List<GmailFolderListDTO>>> WatchGmail(List<string> labelIds, string positionPublicId)
        {
            var jwtResult = await HttpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
            var cookieResult = await HttpContext.AuthenticateAsync("GoogleCookie");

            if (!jwtResult.Succeeded || !cookieResult.Succeeded)
                return Forbid();

            var jwtUserId = jwtResult.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var cookieUserId = cookieResult.Properties.Items["UserId"];

            if (jwtUserId == null || cookieUserId == null || jwtUserId != cookieUserId)
                return Forbid();

            var credential = await _gService.GetOrRefreshTokensAsync(jwtUserId);

            if (credential == null)
                return Unauthorized("Tokens does not exist for current user");

            try
            {
                return await _gService.WatchLabels(credential,labelIds, positionPublicId, jwtUserId);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("Unsubscribe")]
        [Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{"GoogleCookie"}")]
        
        public async Task<IActionResult> Unsubscribe(string publicPosId)
        {
            var jwtResult = await HttpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
            var cookieResult = await HttpContext.AuthenticateAsync("GoogleCookie");

            if (!jwtResult.Succeeded || !cookieResult.Succeeded)
                return Forbid();

            var jwtUserId = jwtResult.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var cookieUserId = cookieResult.Properties.Items["UserId"];

            if (jwtUserId == null || cookieUserId == null || jwtUserId != cookieUserId)
                return Forbid();

            var credential = await _gService.GetOrRefreshTokensAsync(jwtUserId);

            if (credential == null)
                return Unauthorized("Tokens does not exist for current user");

            try
            {
                var result = await _gService.Unsubscribe(credential, publicPosId, jwtUserId);
                if(result)
                return Ok();
                else
                    return StatusCode(500,"Internal Server Error");

            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }


        }



        [HttpPost("Push")]
        [AllowAnonymous]
        public async Task<IActionResult> GmailPush([FromBody] GmailPushDTO envelope)
        {
            var raw = envelope.Message.Data;
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(raw));
            var notif = JsonSerializer.Deserialize<GmailPushNotificationDTO>(json)
                           ?? throw new Exception("Invalid push payload");

            var email = notif.EmailAddress;
            
            var newHist = notif.HistoryId;

            var subs = _context.IntegrationSubscriptions
                       .Where(s => s.Provider == "Gmail" && s.Email == email)
                       .ToList();

            if (subs == null)
            {
                _logger.LogWarning("No subscription found for {Email}, ignoring push", email);
                return Ok();  
            }

            foreach (var s in subs)
            {
               await  _queue.EnqueueAsync(new PushJobDTO{
                   Provider = "Gmail",
                   SubscriptionId = s.Id.ToString(),
                   ResourceId = s.LabelId,
               });
                
            }
            return Accepted();
        }

    }
}
