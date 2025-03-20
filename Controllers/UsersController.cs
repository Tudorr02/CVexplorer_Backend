using AutoMapper;
using CVexplorer.Data;
using CVexplorer.Exceptions;
using CVexplorer.Models.Domain;
using CVexplorer.Models.DTO;
using CVexplorer.Models.DTO.Admin;
using CVexplorer.Repositories.Implementation;
using CVexplorer.Repositories.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace CVexplorer.Controllers
{
    [Authorize(Policy = "RequireAllRoles")]
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController(IUserDetailsRepository _userDetails, UserManager<User> _userManager , ICompanyUsersRepository _companyUser, DataContext _context) : Controller
    {

        [Authorize(Policy = "RequireHRLeaderRole")]
        [HttpGet()]
        public async Task<ActionResult<List<CompanyUsersDTO>>> GetUsers()
        {
            try
            {

                var hrLeader = await _context.Users
                    .Include(u => u.Company) 
                    .FirstOrDefaultAsync(u => u.Id == Convert.ToInt32(_userManager.GetUserId(User)));

                if (hrLeader == null)
                {
                    return Unauthorized(new { error = "User not found or not authenticated." });
                }

                
                
                if (hrLeader.Company == null)
                {
                    return Forbid();
                }

                var users = await _companyUser.GetCompanyUsers(hrLeader.Company.Name);
                return Ok(users);
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { error = ex.Message }); // 404 if company not found
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message }); // 400 for other failures
            }
        }
        
        [HttpGet("Me")]
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

        
        [HttpPut("Me")]
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
