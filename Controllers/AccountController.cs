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


            return new AccountDTO
            {
                Username = user.UserName,
                Token = await tokenService.CreateToken(user),
            };
        }
    }
}
