using CVexplorer.Data;
using CVexplorer.Enums;
using CVexplorer.Models.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.Design;

namespace CVexplorer.Controllers
{
    [Authorize(Policy = "RequireHRUserRole")]
    [ApiController]
    [Route("api/[controller]")]
    public class ChartsController(DataContext _context, UserManager<User> _userManager) : Controller
    {
       

        [HttpGet("GetCandidatesSeniority")]
        public async Task<IActionResult> GetCandidatesSeniority(string? positionPublicId = null, int? departmentId = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized("User not found");

            var allLevels = Enum
                .GetValues(typeof(PositionLevel))
                .Cast<PositionLevel>()
                .Select(lvl => lvl.ToString());

            if (user.CompanyId == null)
            {
                var emptyResult = allLevels.ToDictionary(level => level, level => 0);
                return Ok(emptyResult);
            }

            var companyId = user.CompanyId.Value;
            var query = _context.CVs
                .Include(c => c.Position)
                    .ThenInclude(p => p.Department)
                .Include(c => c.Evaluation)
                .Where(c => c.Position.Department.CompanyId == companyId)
                .AsQueryable();

            if (!string.IsNullOrEmpty(positionPublicId))
                query = query.Where(c => c.Position.PublicId == positionPublicId);

            if (departmentId.HasValue)
                query = query.Where(c => c.Position.DepartmentId == departmentId.Value);

            var levelList = await query
             .Select(c => c.Evaluation.Level.Value)
             .ToListAsync();

            var rawCounts = levelList
                .GroupBy(lvl => lvl)
                .Select(g => new
                {
                    Level = g.Key,
                    Count = g.Count()
                })
                .ToList();

            var result = allLevels.ToDictionary(
                level => level,
                level => rawCounts.FirstOrDefault(rc => rc.Level.ToString() == level)?.Count ?? 0
            );

            return Ok(result);
        }

        [HttpGet("GetScoreDistribution")]
        public async Task<IActionResult> GetScoreDistribution(string? positionPublicId = null, int? departmentId = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized("User not found");

            var bins = new[] { "0-25", "25-50", "50-75", "75-100" };

            if (user.CompanyId == null)
            {
                var emptyBins = bins.ToDictionary(bin => bin, bin => 0);
                return Ok(emptyBins);
            }

            var companyId = user.CompanyId.Value;
            var query = _context.CVs
                .Include(c => c.Position)
                    .ThenInclude(p => p.Department)
                .Where(c => c.Position.Department.CompanyId == companyId)
                .AsQueryable();

            if (!string.IsNullOrEmpty(positionPublicId))
                query = query.Where(c => c.Position.PublicId == positionPublicId);
            if (departmentId.HasValue)
                query = query.Where(c => c.Position.DepartmentId == departmentId.Value);

            var scores = await query
                .Select(c => c.Score)
                .ToListAsync();

            var distribution = new Dictionary<string, int>
            {
                ["0-25"] = scores.Count(s => s >= 0 && s < 25),
                ["25-50"] = scores.Count(s => s >= 25 && s < 50),
                ["50-75"] = scores.Count(s => s >= 50 && s < 75),
                ["75-100"] = scores.Count(s => s >= 75 && s <= 100)
            };

            return Ok(distribution);
        }
    }
}
