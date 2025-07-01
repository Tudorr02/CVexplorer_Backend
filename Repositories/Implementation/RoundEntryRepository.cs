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

        public async Task<bool> UpdateAsync(int reId, int targetStageOrdinal)
        {
            var roundEntry = await _context.RoundEntries
            .Include(re => re.Stage)            
            .FirstOrDefaultAsync(re => re.Id == reId);
            if (roundEntry == null) return false;

            var currentRoundId = roundEntry.Stage.RoundId;

            var targetStage = await _context.RoundStages
            .FirstOrDefaultAsync(s =>
                s.RoundId == currentRoundId &&
                s.Ordinal == targetStageOrdinal);

            if (targetStage == null)
                return false;

            if (roundEntry.StageId == targetStage.Id)
            {

                return true;
            }

            roundEntry.StageId = targetStage.Id;
            _context.RoundEntries.Update(roundEntry);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<string> UpdateDetails(int reId, string details)
        {
            var roundEntry = await _context.RoundEntries
            .Include(re => re.Stage)           
            .FirstOrDefaultAsync(re => re.Id == reId);
            if (roundEntry == null) return null;

            roundEntry.Details = details;

            _context.RoundEntries.Update(roundEntry);
            await _context.SaveChangesAsync();
            return roundEntry.Details;
        }


    }
}
