using CVexplorer.Models.Domain;
using CVexplorer.Models.DTO;

namespace CVexplorer.Repositories.Interface
{
    public interface IRoundEntryRepository
    {
        Task<IEnumerable<RoundEntryListDTO>> GetAllAsync(string roundId);
        Task<CvEvaluationDTO> GetRoundEntryAsync(int reId);

        Task CreateAsync(int roundId , int cvId );
        Task<bool> UpdateAsync(int reId, bool selected);
    }
}
