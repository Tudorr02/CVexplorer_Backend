using AutoMapper;
using CVexplorer.Models.Domain;
using CVexplorer.Models.DTO;
using CVexplorer.Repositories.Implementation;
using CVexplorer.Repositories.Interface;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CVexplorer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountController(UserManager<User> userManager, ITokenService tokenService, IMapper mapper) : Controller
    {
        [HttpPost("Login")]
        public async Task<ActionResult<AccountDTO>> Login(LoginDTO loginDto)
        {
            var user = await userManager.Users.SingleOrDefaultAsync(x => x.NormalizedUserName == loginDto.Username.ToUpper());

            if (user == null) return Unauthorized("Invalid username or password !");

            var result = await userManager.CheckPasswordAsync(user, loginDto.Password);

            if (!result) return Unauthorized("Invalid username or password !");


            // 1. Crează JWT-ul
            var token = await tokenService.CreateToken(user);

            // 2. Pune-l în cookie HttpOnly
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,               // doar HTTPS
                SameSite = SameSiteMode.None,  // ca să meargă cross‐site (popup)
                Expires = DateTime.UtcNow.AddDays(7)
            };
            Response.Cookies.Append("jwt", token, cookieOptions);

            // 3. Returnează chiar și DTO-ul dacă vrei (sau poți returna doar Ok())
            return Ok(new AccountDTO
            {
                Username = user.UserName,
                Token = token   // optional: front‐end nu mai are nevoie să-l stocheze
            });

            //return new AccountDTO
            //{
            //    Username = user.UserName,
            //    Token = await tokenService.CreateToken(user),
            //};
        }
    }
}
