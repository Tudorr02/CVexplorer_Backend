using AutoMapper;
using CVexplorer.Data;
using CVexplorer.Exceptions;
using CVexplorer.Models.Domain;
using CVexplorer.Models.DTO;

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
    public class UsersController(IUserDetailsRepository _userDetails, UserManager<User> _userManager , IUserRepository _userRepository, DataContext _context) : Controller
    {
        [Authorize(Policy = "RequireHRLeaderRole")]
        private async Task<bool> ValidateHRLeaderAsync(int? userId = null, UserDTO? dto = null)
        {
            var hrLeader = await _userManager.GetUserAsync(User);
            if (hrLeader == null)
            {
                throw new UnauthorizedAccessException("User not found or not authenticated.");
            }

            if (hrLeader.CompanyId == null)
            {
                throw new UnauthorizedAccessException("You are not assigned to a company.");
            }

            if (userId.HasValue && dto != null)
            {
                var targetUser = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == userId.Value);

                if (targetUser == null)
                {
                    throw new NotFoundException("User not found.");
                }

                if (targetUser.CompanyId != hrLeader.CompanyId)
                {
                    throw new UnauthorizedAccessException("You are not allowed to modify this user.");
                }
            }

            return true; // ✅ Validation passed
        }


        [Authorize(Policy = "RequireHRLeaderRole")]
        [HttpGet]
        public async Task<ActionResult<List<UserListDTO>>> GetUsers()
        {
            try
            {
                

                var hrLeader = await _userManager.GetUserAsync(User);

                await ValidateHRLeaderAsync();

                var users = await _userRepository.GetUsersAsync((int)hrLeader.CompanyId);
                return Ok(users);
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [Authorize(Policy = "RequireHRLeaderRole")]
        [HttpPut("{userId}")]
        public async Task<ActionResult<UserDTO>> UpdateUser(int userId, [FromBody] UserDTO dto)
        {
            try
            {
                await ValidateHRLeaderAsync(userId, dto);

                var updatedUser = await _userRepository.UpdateUserAsync(userId, dto);

                return Ok(updatedUser);
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [Authorize(Policy = "RequireHRLeaderRole")]
        [HttpDelete("{userId}")]
        public async Task<IActionResult> DeleteUser(int userId)
        {
            try
            {
                await ValidateHRLeaderAsync(userId);

                var isDeleted = await _userRepository.DeleteUserAsync(userId);

                if (!isDeleted)
                {
                    return BadRequest(new { error = "Failed to delete user." });
                }

                return Ok(new { message = "User deleted successfully." });
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [Authorize(Policy = "RequireHRLeaderRole")]
        [HttpPost]
        public async Task<ActionResult> EnrollUser([FromBody] UserEnrollDTO dto)
        {
            try
            {
                
                var hrLeader = await _userManager.GetUserAsync(User);

                await ValidateHRLeaderAsync();

                dto.CompanyName = hrLeader.CompanyId.ToString();

                var result = await _userRepository.EnrollUserAsync((int)hrLeader.CompanyId,dto);

                if(result.Equals(false))
                {
                    return BadRequest(new { error = "Failed to enroll user." });
                }

                return Ok();
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "An unexpected error occurred.", details = ex.Message });
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
