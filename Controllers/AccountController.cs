using AutoMapper;
using CVexplorer.Data;
using CVexplorer.Exceptions;
using CVexplorer.Models.Domain;
using CVexplorer.Models.DTO;
using CVexplorer.Repositories.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace CVexplorer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountController(UserManager<User> userManager, ITokenService tokenService , IMapper mapper, IUserDetailsRepository _userDetails) : Controller
    {
        [Authorize(Policy = "RequireModeratorRole")]
        [HttpPost("Register")]
        public async Task<ActionResult<UserDTO>> Register(RegisterDTO registerDto)
        {
            
            if(await UserExists(registerDto.Username)) return BadRequest("Username is taken");

            var user = mapper.Map<User>(registerDto);

            user.UserName = registerDto.Username.ToLower();

            var result = await userManager.CreateAsync(user, registerDto.Password);
            
            if (!result.Succeeded) return BadRequest("Failed to register");

            await userManager.AddToRoleAsync(user, "HRUser");

            return new UserDTO
            {
                Username = user.UserName,
                Token = await tokenService.CreateToken(user),
            };
        }

        [HttpPost("Login")]
        public async Task<ActionResult<UserDTO>> Login(LoginDTO loginDto)
        {
            var user = await userManager.Users.SingleOrDefaultAsync(x => x.NormalizedUserName == loginDto.Username.ToUpper());

            if (user == null) return Unauthorized("Invalid username or password !");

            var result = await userManager.CheckPasswordAsync(user, loginDto.Password);

            if (!result) return Unauthorized("Invalid username or password !");


            return new UserDTO
            {
                Username = user.UserName,
                Token = await tokenService.CreateToken(user),
            };
        }
       
        private async Task<bool> UserExists(string username)
        {
            return await userManager.Users.AnyAsync(x => x.NormalizedUserName.ToLower() == username.ToLower());
        }

        [Authorize(Policy = "RequireAllRoles")]
        [HttpGet("Details")]
        public async Task<ActionResult<UserDetailsDTO>> GetUserDetails()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized("Invalid token. You do not have permission to access this resource!");

            var userDetails = await _userDetails.GetUserDetailsAsync(int.Parse(userId));
            if (userDetails == null)
                return NotFound(new { message = "User not found." });

            return Ok(userDetails);
        }

        [Authorize(Policy = "RequireAllRoles")]
        [HttpPut("Details")]
        public async Task<ActionResult<UserDetailsDTO>> UpdateUserDetails([FromBody] UserDetailsDTO userDetails)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized("Invalid token. You do not have permission to access this resource!");

            try
            {
                await _userDetails.UpdateUserDetailsAsync(int.Parse(userId), userDetails);
                return Ok(new { message = "User details updated successfully." });
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An unexpected error occurred.", details = ex.Message });
            }
        }
    }
}
