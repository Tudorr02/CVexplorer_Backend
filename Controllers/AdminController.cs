using AutoMapper;
using CVexplorer.Data;
using CVexplorer.Exceptions;
using CVexplorer.Models.Domain;
using CVexplorer.Models.DTO;
using CVexplorer.Models.DTO.Admin;
using CVexplorer.Repositories.Implementation;
using CVexplorer.Repositories.Implementation.Admin;
using CVexplorer.Repositories.Interface;
using CVexplorer.Repositories.Interface.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CVexplorer.Controllers
{
    [Authorize(Policy = "RequireModeratorRole")] // ✅ Restrict access to Admins only
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController(DataContext _context,UserManager<User> _userManager, IUserManagement _userManagement , IMapper _mapper , ITokenService _tokenService) : Controller
    {
        [HttpGet("Users")]
        public async Task<ActionResult<List<UserManagementDTO>>> GetUsers()
        {
            var users = await  _userManagement.GetUsersAsync();
            return Ok(users);

        }

        [HttpPut("Users/{username}")]
        public async Task<IActionResult> UpdateUser(string username,[FromBody] UserManagementDTO dto)
        {
            try
            {
                var updatedUser = await _userManagement.UpdateUserAsync(username,dto);
                return Ok(updatedUser); // Returns updated user details
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { error = ex.Message }); // 404 if user or company not found
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message }); //  400 for other failures
            }
        }

        [HttpGet("Users/{username}")]
        public async Task<ActionResult<UserManagementDTO>> GetUser(string username)
        {
            try
            {
                var user = await _userManagement.GetUserAsync(username);
                return Ok(user);
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { error = ex.Message }); // 404 if user not found
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message }); //  400 for other failures
            }

        }

        [HttpDelete("Users/{username}")]
        public async Task<IActionResult> DeleteUser(string username)
        {
            try
            {
                var deletedUser = await _userManagement.DeleteUserAsync(username);
                return Ok(deletedUser); // Returns deleted user details
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { error = ex.Message }); // 404 if user not found
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message }); //  400 for other failures
            }
        }

        [HttpPost("EnrollUser")]
        public async Task<ActionResult<UserDTO>> EnrollUser(UserEnrollmentDTO dto)
        {
            if (await UserExists(dto.Username))
                return BadRequest("Username is taken");

            // ✅ Validate company before creating user
            int? companyId = null;
            if (!string.IsNullOrWhiteSpace(dto.CompanyName))
            {
                var company = await _context.Companies.FirstOrDefaultAsync(c => c.Name == dto.CompanyName);
                if (company == null)
                    return BadRequest($"Company '{dto.CompanyName}' not found");

                companyId = company.Id; // ✅ Store company ID
            }

            // ✅ Validate roles before creating user
            var rolesToAssign = dto.UserRoles != null && dto.UserRoles.Any() ? dto.UserRoles : new List<string> { "HRUser" };
            var validRoles = await _context.Roles.Select(r => r.Name).ToListAsync();
            var invalidRoles = rolesToAssign.Except(validRoles).ToList();

            if (invalidRoles.Any())
                return BadRequest($"Invalid roles: {string.Join(", ", invalidRoles)}");

            // ✅ Create the user only after validations pass
            var user = _mapper.Map<User>(dto);
            user.UserName = dto.Username.ToLower();
            user.CompanyId = companyId; // ✅ Assign validated company

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
                return BadRequest($"Failed to register: {string.Join(", ", result.Errors.Select(e => e.Description))}");

            var addRolesResult = await _userManager.AddToRolesAsync(user, rolesToAssign);
            if (!addRolesResult.Succeeded)
                return BadRequest("Failed to assign roles.");

            return new UserDTO
            {
                Username = user.UserName,
                Token = await _tokenService.CreateToken(user),
            };
        }

        private async Task<bool> UserExists(string username)
        {
            return await _userManager.Users.AnyAsync(x => x.NormalizedUserName.ToLower() == username.ToLower());
        }


    }
}
