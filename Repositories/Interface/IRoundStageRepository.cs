using CVexplorer.Models.Domain;
using CVexplorer.Models.DTO;

namespace CVexplorer.Repositories.Interface
{
    public interface IRoundStageRepository
    {
        Task<List<RoundStageDTO>> GetAll(string roundPublicId);
        Task<RoundStageDTO> CreateAsync(string roundPublicId , string name);

        Task DeleteLastAsync(string roundPublicId, int ordinal);
    }
}
