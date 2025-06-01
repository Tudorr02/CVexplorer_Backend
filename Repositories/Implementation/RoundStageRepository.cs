using CVexplorer.Data;
using CVexplorer.Models.DTO;
using CVexplorer.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace CVexplorer.Repositories.Implementation
{
    public class RoundStageRepository(DataContext _context) : IRoundStageRepository
    {
        public async Task<List<RoundStageListDTO>> GetAll(string roundPublicId)
        {
            var stages = _context.RoundStages
                .Where(rs => rs.Round.PublicId == roundPublicId)
                .Select(rs => new RoundStageListDTO
                {
                    Name = rs.Name,
                    Ordinal = rs.Ordinal,
                    Entries = _context.RoundEntries
                        .Where(re => re.StageId == rs.Id)
                        .Select(re => new RoundEntryListDTO
                        {
                            Id = re.Id,
                            CandidateName = re.Cv.Evaluation.CandidateName,
                            Score = Convert.ToInt16(re.Cv.Score),
                        })
                        .ToList()
                });
                

            return await stages.ToListAsync();
        }

    }
}
