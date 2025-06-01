using AutoMapper;
using CVexplorer.Data;
using CVexplorer.Models.Domain;
using CVexplorer.Models.DTO;
using CVexplorer.Repositories.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CVexplorer.Controllers
{
    [Authorize(Policy = "RequireHRUserRole")]
    [ApiController]
    [Route("api/[controller]")]
    public class RoundsController(DataContext _context , IRoundEntryRepository _rEntryRepo, IRoundRepository _roundRepository ,UserManager<User> _userManager ,IMapper _mapper) : Controller
    {
        private async Task<bool> IsUserAuthorizedAsync(string? positionPublicId= null, string? roundPublicId = null, int? departmentid = null)
        {
            var userId = _userManager.GetUserId(User);
            var user = await _userManager.Users
                .Include(u => u.UserDepartmentAccesses)
                .FirstOrDefaultAsync(u => u.Id == (userId != null ? int.Parse(userId) : -1));

            if (user == null || user.CompanyId == null)
                return false;

            if (departmentid != null)
            {
                var department = await _context.Departments
                    .Include(d => d.Company)
                    .FirstOrDefaultAsync(d => d.Id == departmentid);
                if (department == null)
                    return false;
                if (department.CompanyId != user.CompanyId)
                    return false;

                if (User.IsInRole("HRUser"))
                {
                    bool hasAccess = user.UserDepartmentAccesses.Any(a => a.DepartmentId == department.Id);
                    if (!hasAccess)
                        return false;
                }



                return true;
            }
            if (roundPublicId != null)
            {
                var round = await _context.Rounds
                    .Include(c => c.Position)
                    .ThenInclude(p => p.Department)
                    .FirstOrDefaultAsync(c => c.PublicId == roundPublicId);

                if (round == null)
                    return false;

                positionPublicId = round.Position.PublicId;
            }


            var position = await _context.Positions
                .Include(p => p.Department)
                .FirstOrDefaultAsync(p => p.PublicId == positionPublicId);

            if (position == null)
                return false;

            if (position.Department.CompanyId != user.CompanyId)
                return false;

            if (User.IsInRole("HRUser"))
            {
                bool hasAccess = user.UserDepartmentAccesses.Any(a => a.DepartmentId == position.DepartmentId);
                if (!hasAccess)
                    return false;
            }

            return true;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<RoundListDTO>>> GetAllRounds(int? departmentId = null,string? publicPositionId = null)
        {
           
            if (departmentId == null && publicPositionId == null)
                return Forbid();

            
            if (publicPositionId != null)
            {
                if (!await IsUserAuthorizedAsync(publicPositionId, null, null))
                    return Forbid();
            }
            // 3) Else if filtering by department, enforce department‐level auth
            else if (departmentId != null)
            {
                if (!await IsUserAuthorizedAsync(null , null , departmentId.Value))
                    return Forbid();
            }

            var rounds = await _roundRepository.ListAsync(departmentId, publicPositionId);

            if (rounds == null)
                return NotFound();

            return Ok(rounds);
        }

        [HttpDelete("{publicId}")]
        public async Task<IActionResult> DeleteRound(string publicId)
        {
            if (!await IsUserAuthorizedAsync(null,publicId , null))
                return Forbid();
            await _roundRepository.DeleteAsync(publicId);
            return NoContent();
        }

        [HttpGet("{publicId}")]
        public async Task<ActionResult<IEnumerable<RoundEntryListDTO>>> GetRound(string publicId)
        {
            if (!await IsUserAuthorizedAsync(null, publicId, null))
                return Forbid();

            //var list = await _rEntryRepo.GetAllAsync(publicId);
            //if (list == null) return NotFound();
            //return Ok(list);
            return null;
        }

    }
}
