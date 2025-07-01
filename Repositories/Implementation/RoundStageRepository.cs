using CVexplorer.Data;
using CVexplorer.Models.Domain;
using CVexplorer.Models.DTO;
using CVexplorer.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace CVexplorer.Repositories.Implementation
{
    public class RoundStageRepository(DataContext _context) : IRoundStageRepository
    {
        public async Task<List<RoundStageDTO>> GetAll(string roundPublicId)
        {
            
            var position = await _context.Rounds
                .Where(p => p.PublicId == roundPublicId)
                .Select(p => new { p.Id, p.Name })
                .FirstOrDefaultAsync() ?? throw new ArgumentException("Position does not exist");

            var stages = _context.RoundStages
                .Include(rs => rs.Entries)
                .Where(rs => rs.Round.PublicId == roundPublicId)
                .Select(rs => new RoundStageDTO
                {
                    Name = rs.Name,
                    Ordinal = rs.Ordinal,
                    Entries = rs.Entries
                        .Select(re => new RoundEntryListDTO
                        {
                            Id = re.Id,
                            CandidateName = re.Cv.Evaluation.CandidateName,
                            Score = Convert.ToInt16(re.Cv.Score),
                            PublicCvId = re.Cv.PublicId,
                            Details = re.Details

                        })
                        .ToList()
                });
                

            return await stages.ToListAsync();
        }

        public async Task<RoundStageDTO> CreateAsync(string roundPublicId, string name)
        {
            var round = await _context.Rounds
                .Include(r => r.Stages)
                .FirstOrDefaultAsync(r => r.PublicId == roundPublicId);

            if (round == null)
                throw new ArgumentException($"Round with PublicId '{roundPublicId}' was not found.");


            var nextOrdinal = round.Stages.Any()
                ? round.Stages.Max(s => s.Ordinal) + 1
                : 0;

            var newStage = new RoundStage
            {
                RoundId = round.Id,
                Name = name,
                Ordinal = nextOrdinal,
                IsActive = true
            };

            _context.RoundStages.Add(newStage);
            await _context.SaveChangesAsync();

            var dto = new RoundStageDTO
            {

                Name = newStage.Name,
                Ordinal = newStage.Ordinal,
                Entries = new List<RoundEntryListDTO>()
            };

            return dto;
        }


        public async Task DeleteLastAsync(string roundPublicId, int ordinal)
        {
            var stageToDelete = await _context.RoundStages
                .Include(s => s.Entries)
                .Include(s => s.Round)
                .FirstOrDefaultAsync(s =>
                    s.Round.PublicId == roundPublicId &&
                    s.Ordinal == ordinal);

            if (stageToDelete == null)
                throw new ArgumentException($"RoundStage cu Ordinal = {ordinal} pentru Round '{roundPublicId}' nu există.");

            var maxOrdinal = await _context.RoundStages
                .Where(s => s.RoundId == stageToDelete.RoundId)
                .MaxAsync(s => s.Ordinal);

            if (ordinal != maxOrdinal)
                throw new InvalidOperationException("Se poate șterge doar ultima etapă (ultima în ordinea Ordinal).");

            if (ordinal == 0 && maxOrdinal == 0)
                throw new InvalidOperationException("Nu se poate șterge singura etapă rămasă în rundă.");

            var previousStage = await _context.RoundStages
                .FirstOrDefaultAsync(s =>
                    s.RoundId == stageToDelete.RoundId &&
                    s.Ordinal == maxOrdinal - 1);

            if (previousStage == null)
                throw new InvalidOperationException(
                    $"Nu s-a găsit etapa cu Ordinal = {maxOrdinal - 1} în Round '{roundPublicId}'.");

            foreach (var entry in stageToDelete.Entries)
            {
                entry.StageId = previousStage.Id;
            }

            _context.RoundStages.Remove(stageToDelete);
            await _context.SaveChangesAsync();
        }

    }
}
