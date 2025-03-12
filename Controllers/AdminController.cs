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
    [Authorize(Policy = "RequireModeratorRole")]
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController(DataContext _context,UserManager<User> _userManager, IDepartmentManagementRepository _departmentManagement ,ICompanyManagement _companyManagement ,IUserManagement _userManagement , IMapper _mapper , ITokenService _tokenService) : Controller
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
                var currentUser = await _userManager.GetUserAsync(User);
                var userRoles = await _userManager.GetRolesAsync(currentUser);

                bool isModerator = userRoles.Contains("Moderator") && !userRoles.Contains("Admin");

                // ✅ Prevent Moderators from assigning Admin role
                if (isModerator && dto.UserRoles.Contains("Admin"))
                {
                    return Forbid("You are not allowed to assign the Admin role.");
                }

                if (username.ToLower() == "admin")
                {
                    return Forbid(); // Prevent modifications to the "admin" user
                }

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
                if (username.ToLower() == "admin")
                {
                    return Forbid(); // Prevent deletion of the "admin" user
                }

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

        [HttpPost("Users")]
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

        [HttpGet("Companies")]
        public async Task<ActionResult<List<GetCompaniesDTO>>> GetCompanies()
        {
            var companies = await _companyManagement.GetCompaniesAsync();
            return Ok(companies);
        }

        [HttpGet("Companies/{companyName}")]
        public async Task<ActionResult<GetCompaniesDTO>> GetCompany(string companyName)
        {
            try
            {
                var company = await _companyManagement.GetCompanyAsync(companyName);
                return Ok(company);
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { error = ex.Message }); // 404 if company not found
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message }); //  400 for other failures
            }
        }

        [HttpPut("Companies/{companyName}")]
        public async Task<IActionResult> UpdateCompany(string companyName, [FromBody] CompanyManagementDTO dto)
        {
            try
            {
                var updatedCompany = await _companyManagement.UpdateCompanyAsync(companyName, dto);
                return Ok(updatedCompany); // Returns updated company details
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { error = ex.Message }); // 404 if company not found
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message }); //  400 for other failures
            }
        }

        [HttpDelete("Companies/{companyName}")]
        public async Task<IActionResult> DeleteCompany(string companyName)
        {
            try
            {
                var isDeleted = await _companyManagement.DeleteCompanyAsync(companyName);
                return Ok(isDeleted); // Returns true if company is deleted
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { error = ex.Message }); // 404 if company not found
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message }); //  400 for other failures
            }
        }

        [HttpPost("Companies")]
        public async Task<ActionResult<CompanyManagementDTO>> CreateCompany(CompanyManagementDTO dto)
        {
            try
            {
                // Check if a company with the same name already exists
                var existingCompany = await _context.Companies
                    .FirstOrDefaultAsync(c => c.Name.ToLower() == dto.Name.ToLower());

                if (existingCompany != null)
                {
                    return BadRequest(new { error = $"A company with the name '{dto.Name}' already exists." });
                }

                var company = await _companyManagement.CreateCompanyAsync(dto);
                return Ok(company); // Returns created company details
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message }); //  400 for other failures
            }
        }
        

        [HttpGet("Roles")]
        public async Task<ActionResult<List<string>>> GetRoles()
        {
            try
            {
                var roles = await _context.Roles.Select(r => r.Name).ToListAsync();
                return Ok(roles);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "An error occurred while fetching roles.", details = ex.Message });
            }
        }

    }
}
