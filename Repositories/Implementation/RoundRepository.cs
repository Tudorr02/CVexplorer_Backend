using CVexplorer.Data;
using CVexplorer.Models.Domain;
using CVexplorer.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace CVexplorer.Repositories.Implementation
{
    public class RoundRepository(DataContext _context) : IRoundRepository
    {
        public async Task<Round> CreateAsync(Guid positionId)
        {
            var existingCount = await _context.Rounds
                .CountAsync(r => r.PositionId == positionId);

            var round = new Round
            {
                PositionId = positionId,
                Name = $"Round {existingCount + 1}",
                PublicId = String.Format("{0}{1}", "R", Guid.NewGuid().ToString().Substring(0, 10)),
            };
            _context.Rounds.Add(round);
            await _context.SaveChangesAsync();
            return round;

        }

        public async Task<IEnumerable<Round>> ListAsync(int? departmentId = null, string? publicPositionId = null)
        {
            // start from all rounds, include Position so we can filter by department
            var rounds = _context.Rounds
                .Include(r => r.Position)
                .ThenInclude(p => p.Department)
                .AsQueryable();


            if (!String.IsNullOrEmpty(publicPositionId))
            {
                rounds = rounds.Where(r => r.Position.PublicId == publicPositionId);
                return await rounds
                .OrderBy(r => r.CreatedAt)
                .ToListAsync();
            }
            else if (departmentId.HasValue)
            {
                rounds = rounds.Where(r => r.Position.DepartmentId == departmentId.Value);
                return await rounds
                .OrderBy(r => r.CreatedAt)
                .ToListAsync();
            }

            return null;

        }

        public async Task DeleteAsync(string publicId)
        {
            var round = await _context.Rounds
                .FirstOrDefaultAsync(r => r.PublicId == publicId);
            if (round != null)
            {
                _context.Rounds.Remove(round);
                await _context.SaveChangesAsync();
            }
        }
    }


}
