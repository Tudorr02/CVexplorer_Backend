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
using System.Security.Claims;

namespace CVexplorer.Controllers
{
    [Authorize(Policy = "RequireHRUserRole")]
    [ApiController]
    [Route("api/[controller]")]
    public class DepartmentsController(DataContext _context, IDepartmentRepository _departmentManagement, UserManager<User> _userManager) : Controller
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

        [Authorize(Policy = "RequireHRLeaderRole")]
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
                    return StatusCode(StatusCodes.Status403Forbidden, new { message = "You are not assigned to a company." });

                }

                var userRoles = await _userManager.GetRolesAsync(user);
                var departments = await _departmentManagement.GetDepartmentsAsync((int)user.CompanyId, user.Id, userRoles.Contains("HRLeader"));
                return Ok(departments);
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred.", error = ex.Message });
            }
        }


        /** ✅ GET a specific department */
        [HttpGet("{departmentId}")]
        public async Task<ActionResult<DepartmentDTO>> GetDepartment(int departmentId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized("User not found!");

            if (user.CompanyId == null)
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "You are not assigned to a company." });

            var department = await _departmentManagement.GetDepartmentAsync(departmentId, (int)user.CompanyId);
            if (department == null) return NotFound(new { message = "Department not found!" });

            return Ok(department);
        }

        [Authorize(Policy = "RequireHRLeaderRole")]
        [HttpPost]
        public async Task<ActionResult<DepartmentListDTO>> CreateDepartment([FromBody] DepartmentDTO dto)
        {
            try
            {
                if (dto == null || string.IsNullOrWhiteSpace(dto.Name))
                    return BadRequest(new { message = "Incomplete data provided !" });

                var user = await _userManager.GetUserAsync(User);

                if (user == null) return Unauthorized("User not found!");

                // ✅ Check if the user belongs to the same company
                if (user.CompanyId == null)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new { message = "You are not assigned to a company." });
                }


                // ✅ Check if department already exists in the company
                bool departmentExists = await _context.Departments
                    .AnyAsync(d => d.CompanyId == user.CompanyId && d.Name == dto.Name);

                if (departmentExists)
                {
                    return Conflict(new { message = "A department with this name already exists in your company." });
                }

                var createdDepartment = await _departmentManagement.CreateDepartmentAsync((int)user.CompanyId, dto);
                return Ok(createdDepartment);
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred.", error = ex.Message });
            }


        }



        [Authorize(Policy = "RequireHRLeaderRole")]
        [HttpPut("{departmentId}")]
        public async Task<ActionResult<DepartmentDTO>> UpdateDepartment(int departmentId, [FromBody] DepartmentDTO dto)
        {
            try
            {

                // ✅ Retrieve the user entity from the database
                var user = await _userManager.GetUserAsync(User);

                if (user == null) return Unauthorized("User not found!");

                // ✅ Check if the user belongs to a company
                if (user.CompanyId == null)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new { message = "You are not assigned to a company." });
                }

                // ✅ Call repository method to update the department
                var updatedDepartment = await _departmentManagement.UpdateDepartmentAsync(departmentId, (int)user.CompanyId, dto);

                if (updatedDepartment == null)
                {
                    return NotFound(new { message = "Department not found or you do not have access." });
                }

                return Ok(updatedDepartment);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred.", error = ex.Message });
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

        [Authorize(Policy = "RequireHRLeaderRole")]
        [HttpGet("DepartmentAccessTemplate")]
        public async Task<ActionResult<List<DepartmentAccessDTO>>> GetDepartmentAccess()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return Unauthorized("User not found!");

                if (user.CompanyId == null)
                    return StatusCode(403, new { message = "You are not assigned to a company." });

                var departmentUsers = await _departmentManagement.GetDepartmentAccessAsync((int)user.CompanyId);
                return Ok(departmentUsers);
            }

            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

    }
}
