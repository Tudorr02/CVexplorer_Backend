using CVexplorer.Models.Domain;
using CVexplorer.Models.DTO;

namespace CVexplorer.Repositories.Interface
{
    public interface IRoundStageRepository
    {
        Task<List<RoundStageListDTO>> GetAll(string roundPublicId);
    }
}
