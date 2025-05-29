using CVexplorer.Data;
using CVexplorer.Models.DTO;
using CVexplorer.Repositories.Interface;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using CVexplorer.Models.Domain;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CVexplorer.Controllers
{
    [Authorize(Policy= "RequireHRUserRole")]
    [ApiController]
    [Route("api/[controller]")]
    public class PositionsController(DataContext _context , IPositionRepository _positionRepository, UserManager<User> _userManager) : Controller
    {
        private async Task<bool> IsUserAuthorizedAsync(string? publicId = null,int ? departmentId = null )
        {
            var userId = _userManager.GetUserId(User);
            var user = await _userManager.Users
                .Include(u => u.UserDepartmentAccesses)
                .FirstOrDefaultAsync(u => u.Id == (userId != null ? int.Parse(userId) : -1));

            if (user == null || user.CompanyId == null)
                return false;


            Department department = null;

            if (publicId != null) { 

                var position = await _context.Positions
                    .Include(p => p.Department)
                    .FirstOrDefaultAsync(p => p.PublicId == publicId);


                if (position == null)
                    return false;

                department = position.Department;

            }
            else
            {
                department = await _context.Departments.FirstOrDefaultAsync(d => d.Id == departmentId);

            }

            if (department == null)
                return false;

            // ✅ Verificare companie
            if (department.CompanyId != user.CompanyId)
                return false;

            // ✅ Dacă este HRUser, verificăm accesul la departament
            if (User.IsInRole("HRUser"))
            {
                bool hasDepartmentAccess = user.UserDepartmentAccesses.Any(a => a.DepartmentId == department.Id);
                if (!hasDepartmentAccess)
                    return false;
            }

            return true;
        }


        [HttpGet("{publicId}")]
        public async Task<ActionResult<PositionDTO>> GetPosition(string publicId)
        {
            if (!await IsUserAuthorizedAsync(publicId))
                return Forbid();

            var result = await _positionRepository.GetPositionAsync(publicId);
            return Ok(result);
        }

        [HttpPost("/api/Departments/{departmentId}/Positions")]
        public async Task<ActionResult<PositionListDTO>> CreatePosition(int departmentId, [FromBody] PositionDTO dto)
        {
            if (!await IsUserAuthorizedAsync(null,departmentId))
                return Forbid();

            var created = await _positionRepository.CreatePositionAsync(departmentId, dto);
            return Ok(created);
        }

        [HttpPut("{publicId}")]
        public async Task<IActionResult> UpdatePosition(string publicId, [FromBody] PositionDTO dto)
        {
            if (!await IsUserAuthorizedAsync(publicId))
                return Forbid();

            var updated = await _positionRepository.UpdatePositionAsync(publicId, dto);
            if (!updated) return NotFound();
            return NoContent();
        }

        [HttpDelete("{publicId}")]
        public async Task<IActionResult> DeletePosition(string publicId)
        {
            if (!await IsUserAuthorizedAsync(publicId))
                return Forbid();

            var deleted = await _positionRepository.DeletePositionAsync(publicId);
            if (!deleted) return NotFound();
            return NoContent();
        }
    }
}
