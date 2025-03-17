using CVexplorer.Data;
using CVexplorer.Exceptions;
using CVexplorer.Models.Domain;
using CVexplorer.Models.DTO;
using CVexplorer.Repositories.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CVexplorer.Controllers
{
    [Authorize(Policy= "RequireHRUserRole")]
    [ApiController]
    [Route("api/[controller]")]
    public class DepartmentsController(DataContext _context,IDepartmentRepository _departmentManagement , UserManager<User> _userManager) : Controller
    {

        [HttpGet("DepartmentsTree")]
        public async Task<ActionResult<List<DepartmentTreeNodeDTO>>> GetDepartmentsTree()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null) return Unauthorized("User not found!");

            if (user.CompanyId == null)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "You are not assigned to a company." });

            }

            var userRoles = await _userManager.GetRolesAsync(user);
            var departments = await _departmentManagement.GetDepartmentsTreeAsync((int)user.CompanyId, user.Id, userRoles.Contains("HRLeader"));

            return Ok(departments);
        }

        [HttpGet]
        public async Task<ActionResult<List<DepartmentListDTO>>> GetDepartments()
        {
            try
            {
           
                // ✅ Retrieve the user entity from the database
                var user = await _userManager.GetUserAsync(User);


                if (user == null) return Unauthorized("User not found!");

                if (user.CompanyId == null)
                {
                    return StatusCode(StatusCodes.Status403Forbidden,new { message = "You are not assigned to a company." });
                   
                }

                var userRoles = await _userManager.GetRolesAsync(user);
                var departments = await _departmentManagement.GetDepartmentsAsync((int)user.CompanyId, user.Id, userRoles.Contains("HRLeader"));
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

        //[HttpGet("{companyName}/departments/{departmentName}")]
        //public async Task<ActionResult<DepartmentDTO>> GetDepartment(string companyName, string departmentName)
        //{
        //    try
        //    {
        //        if(departmentName == null || string.IsNullOrWhiteSpace(departmentName))
        //            return BadRequest(new { message = "Department name is required." });

        //        if(companyName == null || string.IsNullOrWhiteSpace(companyName))
        //            return BadRequest(new { message = "Company name is required." });

        //        // ✅ Extract User ID from JWT Token
        //        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        //        if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized("Invalid token!");

        //        int userId = int.Parse(userIdClaim);

        //        // ✅ Retrieve the user entity from the database
        //        var user = await _userManager.Users
        //            .Include(u => u.Company) // Ensure Company is loaded
        //            .FirstOrDefaultAsync(u => u.Id == userId);

        //        if (user == null) return Unauthorized("User not found!");

        //        // ✅ Check if the user belongs to the same company
        //        if (user.Company == null || !user.Company.Name.Equals(companyName, StringComparison.OrdinalIgnoreCase))
        //        {
        //            return Forbid("You are not authorized to access this company's departments.");
        //        }

        //        var department = await _departmentManagement.GetDepartmentAsync(companyName, departmentName);
        //        return Ok(department);
        //    }
        //    catch (NotFoundException ex)
        //    {
        //        return NotFound(new { message = ex.Message });
        //    }
        //}

        [Authorize(Policy = "RequireHRLeaderRole")]
        [HttpPost]
        public async Task<ActionResult<DepartmentListDTO>> CreateDepartment(string departmentName)
        {
            if (departmentName == null || string.IsNullOrWhiteSpace(departmentName))
                return BadRequest(new { message = "Department name is required." });

            var user = await _userManager.GetUserAsync(User);

            if (user == null) return Unauthorized("User not found!");

            // ✅ Check if the user belongs to the same company
            if (user.CompanyId == null)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "You are not assigned to a company." });
            }

            var createdDepartment = await _departmentManagement.CreateDepartmentAsync((int)user.CompanyId, departmentName);
            return Ok(createdDepartment);
        }

        [Authorize(Policy = "RequireHRLeaderRole")]
        [HttpPut("{departmentId}")]
        public async Task<ActionResult<DepartmentDTO>> UpdateDepartment( int  departmentId, [FromBody] DepartmentDTO dto)
        {
            try
            {
               

                // ✅ Retrieve the user entity from the database
                var user = await _userManager.GetUserAsync(User);

                if (user == null) return Unauthorized("User not found!");

                // ✅ Check if the user belongs to the same company
                if (user.CompanyId == null)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new { message = "You are not assigned to a company." });
                }

                var department = _context.Departments.Include(d => d.Company).FirstOrDefault(d => d.Id == departmentId && d.CompanyId == user.CompanyId);

                if( department == null)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new { message = "This department does exist in your company!" });

                }

                var updatedDepartment = await _departmentManagement.UpdateDepartmentAsync(departmentId, dto);
                return Ok(updatedDepartment);
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [Authorize(Policy = "RequireHRLeaderRole")]
        [HttpDelete("{departmentId}")]
        public async Task<IActionResult> DeleteDepartment(int departmentId)
        {
            try
            {

                // ✅ Retrieve the user entity from the database
                var user = await _userManager.GetUserAsync(User);

                if (user == null) return Unauthorized("User not found!");

                // ✅ Check if the user belongs to the same company
                if (user.CompanyId == null)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new { message = "You are not assigned to a company." });
                }

                var department = _context.Departments.Include(d => d.Company).FirstOrDefault(d => d.Id == departmentId && d.CompanyId == user.CompanyId);

                if (department == null)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new { message = "This department does exist in your company!" });

                }

                var success = await _departmentManagement.DeleteDepartmentAsync(departmentId);
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
