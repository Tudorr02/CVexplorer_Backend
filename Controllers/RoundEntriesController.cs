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
    public class RoundEntriesController(DataContext _context, IRoundEntryRepository _rEntryRepo, UserManager<User> _userManager) : Controller
    {
        private async Task<bool> IsUserAuthorizedAsync(int entryId)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return false;

            var user = await _userManager.Users
                .Include(u => u.UserDepartmentAccesses)
                .FirstOrDefaultAsync(u => u.Id == int.Parse(userId));

            if (user == null || user.CompanyId == null)
                return false;

            var roundId = await _context.RoundEntries.Include(re => re.Stage).ThenInclude(s => s.Round)
                .Where(re => re.Id == entryId)
                .Select(re => re.Stage.RoundId)
                .FirstOrDefaultAsync();


            var round = await _context.Rounds
                .Include(r => r.Position)
                  .ThenInclude(p => p.Department)
                .FirstOrDefaultAsync(r => r.Id == roundId);

            if (round == null)
                return false;

            var department = round.Position.Department;
            if (department.CompanyId != user.CompanyId)
                return false;


            if (User.IsInRole("HRUser"))
            {
                bool hasAccess = user.UserDepartmentAccesses
                    .Any(a => a.DepartmentId == department.Id);

                if (!hasAccess)
                    return false;
            }

            return true;
        }



        [HttpPut("{entryId}")]
        public async Task<IActionResult> UpdateRoundEntry(int entryId, int targetOrdinal)
        {
            if (!await IsUserAuthorizedAsync(entryId))
                return Forbid();

            try
            {
                var updatedEntry = await _rEntryRepo.UpdateAsync(entryId, targetOrdinal);
                if (updatedEntry == null)
                    return NotFound();
                return Ok(updatedEntry);
            }
            catch (Exception ex)
            {
                return BadRequest($"An error occurred while updating the round entry: {ex.Message}");
            }


        }
    }
}
