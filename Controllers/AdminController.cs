using AutoMapper;
using CVexplorer.Data;
using CVexplorer.Exceptions;
using CVexplorer.Models.Domain;
using CVexplorer.Models.DTO;
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
    public class AdminController(DataContext _context,UserManager<User> _userManager, IDepartmentRepository _departmentManagement ,ICompanyManagementRepository _companyManagement ,IUserManagementRepository _userManagement , IMapper _mapper , ITokenService _tokenService) : Controller
    {
        [HttpGet("Users")]
        public async Task<ActionResult<List<UserManagementListDTO>>> GetUsers()
        {
            var currentUser = await _userManager.GetUserAsync(User);

            if(currentUser == null)
                return Unauthorized(new { error = "User not found or not authenticated." });
         

            var users = await  _userManagement.GetUsersAsync();
            return Ok(users);

        }

        [HttpPut("Users/{userId:int}")]
        public async Task<ActionResult<UserManagementDTO>> UpdateUser(int userId, [FromBody] UserManagementDTO dto)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);

                if (currentUser == null)
                    return Unauthorized(new { error = "User not found or not authenticated." });

                bool isModerator = await _userManager.IsInRoleAsync(currentUser, "Moderator");


                // ✅ Prevent Moderators from assigning Admin role
                if (isModerator && dto.UserRole.Equals("Admin"))
                    return Forbid("You are not allowed to assign the Admin role.");
                

                // ✅ Prevent modifications to the built-in "admin" user
                var userToUpdate = await _userManager.FindByIdAsync(userId.ToString());
                if (userToUpdate == null)
                {
                    return NotFound(new { error = "User not found" });
                }
                if (userToUpdate.UserName.ToLower() == "admin")
                {
                    return Forbid();
                }

                var updatedUser = await _userManagement.UpdateUserAsync(userId,dto);
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

        [HttpDelete("Users/{userId:int}")]
        public async Task<IActionResult> DeleteUser(int userId)
        {
            try
            {
                var userToDelete = await _userManager.FindByIdAsync(userId.ToString());

                if (userToDelete == null)
                {
                    return NotFound(new { error = "User not found" });
                }

                if (userToDelete.UserName?.ToLower() == "admin")
                {
                    return Forbid(); // Prevent deletion of the "admin" user
                }
                var deletedUser = await _userManagement.DeleteUserAsync(userId);
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
        public async Task<ActionResult<AccountDTO>> EnrollUser(UserEnrollDTO dto)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);

                if (currentUser == null)
                    return Unauthorized(new { error = "User not found or not authenticated." });

                var result = await _userManagement.EnrollUserAsync(dto);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "An unexpected error occurred." });
            }
        }


        [HttpGet("Companies")]
        public async Task<ActionResult<List<CompanyManagementListDTO>>> GetCompanies()
        {
            var currentUser = await _userManager.GetUserAsync(User);

            if (currentUser == null)
                return Unauthorized(new { error = "User not found or not authenticated." });

            var companies = await _companyManagement.GetCompaniesAsync();
            return Ok(companies);
        }

        [HttpPut("Companies/{companyId:int}")]
        public async Task<ActionResult<CompanyManagementDTO>> UpdateCompany(int companyId, [FromBody] CompanyManagementDTO dto)
        {
            try
            {
                var updatedCompany = await _companyManagement.UpdateCompanyAsync(companyId, dto);
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

        [HttpDelete("Companies/{companyId:int}")]
        public async Task<IActionResult> DeleteCompany(int companyId)
        {
            try
            {
                var isDeleted = await _companyManagement.DeleteCompanyAsync(companyId);
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
