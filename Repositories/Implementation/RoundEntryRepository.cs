using CVexplorer.Data;
using CVexplorer.Enums;
using CVexplorer.Models.Domain;
using CVexplorer.Models.DTO;
using CVexplorer.Models.Primitives;
using CVexplorer.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace CVexplorer.Repositories.Implementation
{
    public class RoundEntryRepository(DataContext _context, IPositionRepository _posRepository) : IRoundEntryRepository
    {
        //public async Task<IEnumerable<RoundEntryListDTO>> GetAllAsync(string roundId)
        //{
        //    return await _context.RoundEntries
        //            .Include(re => re.Cv)
        //             .Include(re => re.Stage)
        //                .ThenInclude(s => s.Round)
        //           .Where(re => re.Stage.Round.PublicId == roundId)
        //           .Select(re => new RoundEntryListDTO
        //           {
        //               Id = re.Id,
        //               CandidateName = re.Cv.Evaluation.CandidateName,
        //               Score = Convert.ToInt16(re.Cv.Score),
        //               StageName = re.Stage.Name,
        //           })
        //           .OrderByDescending(r=>r.Score)
        //           .ToListAsync();
        //}

      

        public async Task CreateAsync (int roundId , int cvId)
        {
            var firstStage = await _context.RoundStages
            .Where(s => s.RoundId == roundId && s.IsActive)
            .OrderBy(s => s.Ordinal)
            .FirstOrDefaultAsync();

            if (firstStage == null)
                throw new InvalidOperationException("No active stages found for this round.");

            _context.RoundEntries.Add( new RoundEntry { StageId = firstStage.Id, CvId = cvId });
            await _context.SaveChangesAsync();
        }

        public async Task<bool> UpdateAsync(int reId, int targetStageId)
        {
            var roundEntry = await _context.RoundEntries.FindAsync(reId);
            if (roundEntry == null) return false;

            var stage = await _context.RoundStages.FindAsync(targetStageId);
            if (stage == null)
                return false;

            roundEntry.StageId = targetStageId;
            _context.RoundEntries.Update(roundEntry);
            await _context.SaveChangesAsync();
            return true;
        }


    }
}
