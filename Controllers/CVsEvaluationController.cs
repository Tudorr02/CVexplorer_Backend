using CVexplorer.Data;
using CVexplorer.Models.Domain;
using CVexplorer.Models.DTO;
using CVexplorer.Repositories.Implementation;
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
    public class CVsEvaluationController(DataContext _context, ICVEvaluationRepository _cvEvaluation, UserManager<User> _userManager) : Controller
    {
        private async Task<bool> IsUserAuthorizedAsync(string positionPublicId, Guid? cvPublicId = null)
        {
            var userId = _userManager.GetUserId(User);
            var user = await _userManager.Users
                .Include(u => u.UserDepartmentAccesses)
                .FirstOrDefaultAsync(u => u.Id == (userId != null ? int.Parse(userId) : -1));

            if (user == null || user.CompanyId == null)
                return false;


            if (cvPublicId != null)
            {
                var cv = await _context.CVs
                    .Include(c => c.Position)
                    .ThenInclude(p => p.Department)
                    .FirstOrDefaultAsync(c => c.PublicId == cvPublicId);

                if (cv == null)
                    return false;

                positionPublicId = cv.Position.PublicId;
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


        [HttpPut("{cvPublicId:guid}")]
        public async Task<ActionResult<CvEvaluationResultDTO>> UpdateEvaluation(Guid cvPublicId, [FromBody] CvEvaluationResultDTO editDto)
        {
            if (!await IsUserAuthorizedAsync(null, cvPublicId))
                return Forbid();

            if (editDto == null)
                return BadRequest("Edit data is required.");

            try
            {
                var updated = await _cvEvaluation.UpdateAsync(cvPublicId, editDto);
                return Ok(updated);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }


        [HttpGet("{cvPublicId:guid}")]
        public async Task<ActionResult<CvEvaluationDTO>> GetEvaluation(Guid cvPublicId)
        {
            if (!await IsUserAuthorizedAsync(null, cvPublicId))
                return Forbid();

            var cv = await _cvEvaluation.GetEvaluationAsync(cvPublicId);

            if (cv == null)
                return NotFound();

            return Ok(cv);
        }

    }
}
