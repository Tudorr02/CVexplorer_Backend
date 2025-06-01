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
        public class RoundStagesController(IRoundStageRepository _rsRepository, UserManager<User> _userManager , DataContext _context) : Controller
        {

            private async Task<bool> IsUserAuthorizedAsync(string roundPublicId )
            {
                var userId = _userManager.GetUserId(User);

                

                var user = await _userManager.Users
                    .Include(u => u.UserDepartmentAccesses)
                    .FirstOrDefaultAsync(u => u.Id == (userId != null ? int.Parse(userId) : -1));

                if (user == null || user.CompanyId == null)
                    return false;


                var round = await _context.Rounds
                    .Include(r => r.Position)
                    .ThenInclude(p => p.Department)
                    .FirstOrDefaultAsync(r => r.PublicId == roundPublicId);

                if (round == null)
                    return false;

                Department department  = round.Position.Department;

                if (department == null)
                    return false;

                if (department.CompanyId != user.CompanyId)
                    return false;

                if (User.IsInRole("HRUser"))
                {
                    bool hasDepartmentAccess = user.UserDepartmentAccesses.Any(a => a.DepartmentId == department.Id);
                    if (!hasDepartmentAccess)
                        return false;
                }

                return true;
            }

            [HttpPost("/api/Rounds/{roundPublicId}/RoundStages")]
            public async Task<ActionResult<RoundStageDTO>> CreateAsync(string roundPublicId, string name)
            {
                if(!await IsUserAuthorizedAsync(roundPublicId))
                {
                    return Forbid("You do not have permission to create a round stage for this round.");
                }

                var roundStage = await _rsRepository.CreateAsync(roundPublicId, name);
                if (roundStage == null)
                {
                    return BadRequest("Failed to create round stage.");
                }
                return Ok(roundStage);
            }

            [HttpDelete("/api/Rounds/{roundPublicId}/RoundStages/{ordinal}")]
            public async Task<IActionResult> DeleteLastAsync(string roundPublicId,int ordinal)
            {
                if (!await IsUserAuthorizedAsync(roundPublicId))
                {
                    return Forbid("You do not have permission to delete this round stage.");
                }
                await _rsRepository.DeleteLastAsync(roundPublicId, ordinal);

                return Ok();
            }
        }
    
}
