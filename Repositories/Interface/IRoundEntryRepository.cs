using CVexplorer.Models.Domain;
using CVexplorer.Models.DTO;

namespace CVexplorer.Repositories.Interface
{
    public interface IRoundEntryRepository
    {
       // Task<IEnumerable<RoundEntryListDTO>> GetAllAsync(string roundId);
      

        Task CreateAsync(int roundId , int cvId );
        Task<bool> UpdateAsync(int reId,int targetStageId);
    }
}
