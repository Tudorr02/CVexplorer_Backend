using CVexplorer.Models.Domain;
using CVexplorer.Models.DTO;

namespace CVexplorer.Repositories.Interface
{
    public interface IRoundEntryRepository
    {
       
      

        Task CreateAsync(int roundId , int cvId );
        Task<bool> UpdateAsync(int reId, int targetStageOrdinal);
    }
}
