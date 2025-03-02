using CVexplorer.Exceptions;
using CVexplorer.Models.Domain;
using CVexplorer.Models.DTO;
using CVexplorer.Repositories.Interface;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CVexplorer.Controllers
{
    public class DepartmentController(IDepartmentManagement _departmentManagement , UserManager<User> _userManager) : Controller
    {

        [HttpGet("{companyName}/departments")]
        public async Task<ActionResult<List<DepartmentManagementDTO>>> GetDepartments(string companyName)
        {
            try
            {
                // ✅ Extract User ID from JWT Token
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized("Invalid token!");

                int userId = int.Parse(userIdClaim);

                // ✅ Retrieve the user entity from the database
                var user = await _userManager.Users
                    .Include(u => u.Company) // Ensure Company is loaded
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null) return Unauthorized("User not found!");

                // ✅ Check if the user belongs to the same company
                if (user.Company == null || !user.Company.Name.Equals(companyName, StringComparison.OrdinalIgnoreCase))
                {
                    return Forbid("You are not authorized to access this company's departments.");
                }

                // ✅ Check if user has the HR Leader role
                bool hrLeader = User.IsInRole("HRLeader");

                var departments = await _departmentManagement.GetDepartmentsAsync(companyName, userId, hrLeader);
                return Ok(departments);
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred.", error = ex.Message });
            }
        }

        [HttpGet("{companyName}/departments/{departmentName}")]
        public async Task<ActionResult<DepartmentManagementDTO>> GetDepartment(string companyName, string departmentName)
        {
            try
            {
                if(departmentName == null || string.IsNullOrWhiteSpace(departmentName))
                    return BadRequest(new { message = "Department name is required." });

                if(companyName == null || string.IsNullOrWhiteSpace(companyName))
                    return BadRequest(new { message = "Company name is required." });

                // ✅ Extract User ID from JWT Token
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized("Invalid token!");

                int userId = int.Parse(userIdClaim);

                // ✅ Retrieve the user entity from the database
                var user = await _userManager.Users
                    .Include(u => u.Company) // Ensure Company is loaded
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null) return Unauthorized("User not found!");

                // ✅ Check if the user belongs to the same company
                if (user.Company == null || !user.Company.Name.Equals(companyName, StringComparison.OrdinalIgnoreCase))
                {
                    return Forbid("You are not authorized to access this company's departments.");
                }

                var department = await _departmentManagement.GetDepartmentAsync(companyName, departmentName);
                return Ok(department);
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpPost("{companyName}/departments")]
        public async Task<ActionResult<DepartmentManagementDTO>> CreateDepartment(string companyName, string departmentName)
        {
            if (departmentName == null || string.IsNullOrWhiteSpace(departmentName))
                return BadRequest(new { message = "Department name is required." });

            if (companyName == null || string.IsNullOrWhiteSpace(companyName))
                return BadRequest(new { message = "Company name is required." });

            // ✅ Extract User ID from JWT Token
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized("Invalid token!");

            int userId = int.Parse(userIdClaim);

            // ✅ Retrieve the user entity from the database
            var user = await _userManager.Users
                .Include(u => u.Company) // Ensure Company is loaded
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) return Unauthorized("User not found!");

            // ✅ Check if the user belongs to the same company
            if (user.Company == null || !user.Company.Name.Equals(companyName, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid("You are not authorized to access this company's departments.");
            }

            var createdDepartment = await _departmentManagement.CreateDepartmentAsync(companyName, departmentName);
            return CreatedAtAction(nameof(GetDepartment), new { companyName, departmentName = createdDepartment.Name }, createdDepartment);
        }

        [HttpPut("{companyName}/departments/{departmentName}")]
        public async Task<ActionResult<DepartmentManagementDTO>> UpdateDepartment(string companyName, string departmentName, [FromBody] DepartmentManagementDTO dto)
        {
            try
            {
                if (departmentName == null || string.IsNullOrWhiteSpace(departmentName))
                    return BadRequest(new { message = "Department name is required." });

                if (companyName == null || string.IsNullOrWhiteSpace(companyName))
                    return BadRequest(new { message = "Company name is required." });

                // ✅ Extract User ID from JWT Token
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized("Invalid token!");

                int userId = int.Parse(userIdClaim);

                // ✅ Retrieve the user entity from the database
                var user = await _userManager.Users
                    .Include(u => u.Company) // Ensure Company is loaded
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null) return Unauthorized("User not found!");

                // ✅ Check if the user belongs to the same company
                if (user.Company == null || !user.Company.Name.Equals(companyName, StringComparison.OrdinalIgnoreCase))
                {
                    return Forbid("You are not authorized to access this company's departments.");
                }

                var updatedDepartment = await _departmentManagement.UpdateDepartmentAsync(companyName, departmentName, dto);
                return Ok(updatedDepartment);
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpDelete("{companyName}/departments/{departmentName}")]
        public async Task<IActionResult> DeleteDepartment(string companyName, string departmentName)
        {
            try
            {
                if (departmentName == null || string.IsNullOrWhiteSpace(departmentName))
                    return BadRequest(new { message = "Department name is required." });

                if (companyName == null || string.IsNullOrWhiteSpace(companyName))
                    return BadRequest(new { message = "Company name is required." });

                // ✅ Extract User ID from JWT Token
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized("Invalid token!");

                int userId = int.Parse(userIdClaim);

                // ✅ Retrieve the user entity from the database
                var user = await _userManager.Users
                    .Include(u => u.Company) // Ensure Company is loaded
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null) return Unauthorized("User not found!");

                // ✅ Check if the user belongs to the same company
                if (user.Company == null || !user.Company.Name.Equals(companyName, StringComparison.OrdinalIgnoreCase))
                {
                    return Forbid("You are not authorized to access this company's departments.");
                }

                var success = await _departmentManagement.DeleteDepartmentAsync(companyName, departmentName);
                if (!success) return BadRequest(new { message = "Failed to delete department." });

                return Ok(new { message = "Department deleted successfully." });
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

    }
}
