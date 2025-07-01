using AutoMapper;
using CVexplorer.Models.Domain;
using CVexplorer.Models.DTO;
using CVexplorer.Repositories.Interface;
using CVexplorer.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static CVexplorer.Services.Implementation.OutlookService;

namespace CVexplorer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountController(UserManager<User> userManager, ITokenService tokenService,IGmailService _gService, IOutlookService _oService ,IMapper mapper, UserManager<User> _userManager) : Controller
    {
        [HttpPost("Login")]
        public async Task<ActionResult<AccountDTO>> Login(LoginDTO loginDto)
        {
            var user = await userManager.Users.SingleOrDefaultAsync(x => x.NormalizedUserName == loginDto.Username.ToUpper());

            if (user == null) return Unauthorized("Invalid username or password !");

            var result = await userManager.CheckPasswordAsync(user, loginDto.Password);

            if (!result) return Unauthorized("Invalid username or password !");

            var token = await tokenService.CreateToken(user);
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,               // only HTTPS
                SameSite = SameSiteMode.None,  
                Expires = DateTime.UtcNow.AddDays(7)
            };
            Response.Cookies.Append("jwt", token, cookieOptions);

            
            return Ok(new AccountDTO
            {
                Username = user.UserName,
                Token = token  
            });

        }

        [HttpPost("Logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {

            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                try
                {
                    var oTokens = await _oService.GetOrRefreshTokensAsync(user.Id.ToString());
                    if (oTokens != null)
                        await _oService.Disconnect(user.Id.ToString(), oTokens);
                }
                catch
                {
                    
                }

                try
                {
                    await _gService.Disconnect(user.Id.ToString());

                }
                catch
                {

                }

                Response.Cookies.Delete("Google.Auth", new CookieOptions
                {
                    Path = "/",
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.None
                });

                Response.Cookies.Delete("Microsoft.Auth", new CookieOptions
                {
                    Path = "/",
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.None
                });

                Response.Cookies.Delete("jwt", new CookieOptions
                {
                    Path = "/",
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.None
                });


            }



            

            return NoContent();
        }
    }
}
