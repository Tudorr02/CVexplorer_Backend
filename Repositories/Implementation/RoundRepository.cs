using CVexplorer.Data;
using CVexplorer.Models.Domain;
using CVexplorer.Models.DTO;
using CVexplorer.Repositories.Interface;
using Microsoft.AspNetCore.Mvc;
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
                Stages = new List<RoundStage>()
                {
                    new RoundStage
                    {
                        Name = "All Candidates"
                    }
                }

            };
          
            _context.Rounds.Add(round);
            await _context.SaveChangesAsync();
            return round;

        }

        public async Task<IEnumerable<RoundListDTO>> ListAsync(int? departmentId = null, string? publicPositionId = null)
        {
            var rounds = _context.Rounds
                .Include(r => r.Position)
                .ThenInclude(p => p.Department)
                .Include(r => r.Stages)
                .ThenInclude(s => s.Entries)
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
                    CreatedAt = r.CreatedAt,
                    CandidatesNumber = r.Stages
                .SelectMany(s => s.Entries)
                .Count(),
                    Stage = r.Stages
                        .OrderByDescending(s => s.Ordinal)
                        .Select(s => s.Name)
                        .FirstOrDefault()
                     ?? string.Empty,
                    PositionName = departmentId.HasValue
                               ? r.Position.Name
                               : null
                })
                .ToListAsync();
        }

        public async Task DeleteAsync(string publicId)
        {
            var round = await _context.Rounds
                .Include(r => r.IntegrationSubscriptions)
                .FirstOrDefaultAsync(r => r.PublicId == publicId);

            if (round != null)
            {
                _context.Rounds.Remove(round);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<ActionResult<List<RoundStageDTO>>> GetAsync(string publicId)
        {
            var round = await _context.Rounds
                .Include(r => r.Stages)
                .ThenInclude(s => s.Entries)
                .ThenInclude(e => e.Cv)
                .FirstOrDefaultAsync(r => r.PublicId == publicId);

            if (round == null)
            {
                return new NotFoundResult();
            }

            var dto = round.Stages
                .OrderBy(s => s.Ordinal)        
                .Select(s => new RoundStageDTO
                {
                    Name = s.Name,
                    Ordinal = s.Ordinal,

                    Entries = s.Entries
                        .Select(e => new RoundEntryListDTO
                        {
                            Id = e.Id,
                            CandidateName = e.Cv.FileName,  
                            Score = Convert.ToInt16(e.Cv.Score)
                        })
                        .ToList()
                })
                .ToList();

            return dto;
        }
    }


}
