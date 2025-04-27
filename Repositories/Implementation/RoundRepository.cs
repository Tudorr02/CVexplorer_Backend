using CVexplorer.Data;
using CVexplorer.Models.Domain;
using CVexplorer.Models.DTO;
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

        public async Task<IEnumerable<RoundListDTO>> ListAsync(int? departmentId = null, string? publicPositionId = null)
        {
            // start from all rounds, include Position so we can filter by department
            var rounds = _context.Rounds
                .Include(r => r.Position)
                .ThenInclude(p => p.Department)
                .Include(r => r.RoundEntries)
                .AsQueryable();


            if (!String.IsNullOrEmpty(publicPositionId))
            {
                rounds = rounds.Where(r => r.Position.PublicId == publicPositionId);

            }
            else if (departmentId.HasValue)
            {
                rounds = rounds.Where(r => r.Position.DepartmentId == departmentId.Value);

            }
            else
                return null;

            return await rounds
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new RoundListDTO
                {
                    PublicId = r.PublicId,
                    Name = r.Name,
                    // … copy over any other Round properties you need …
                    CreatedAt = r.CreatedAt,
                    CandidatesNumber = r.RoundEntries.Count
                })
                .ToListAsync();
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
