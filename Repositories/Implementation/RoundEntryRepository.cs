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
        public async Task<IEnumerable<RoundEntryListDTO>> GetAllAsync(string roundId)
        {
            return await _context.RoundEntries
                    .Include(re => re.Cv)
                    .Include(re => re.Round)
                   .Where(re => re.Round.PublicId == roundId)
                   .Select(re => new RoundEntryListDTO
                   {
                       Id = re.Id,
                       CandidateName = re.Cv.Evaluation.CandidateName,
                       Score = Convert.ToInt16(re.Cv.Score),
                       Selected = re.Selected
                   })
                   .OrderByDescending(r=>r.Score)
                   .ToListAsync();
        }

      

        public async Task CreateAsync (int roundId , int cvId)
        {
            _context.RoundEntries.Add( new RoundEntry { RoundId = roundId, CvId = cvId });
            await _context.SaveChangesAsync();
        }

        public async Task<bool> UpdateAsync(int reId, bool selected)
        {
            var roundEntry = await _context.RoundEntries.FindAsync(reId);
            if (roundEntry == null) return false;
            roundEntry.Selected = selected;
            _context.RoundEntries.Update(roundEntry);
            await _context.SaveChangesAsync();
            return true;
        }


    }
}
