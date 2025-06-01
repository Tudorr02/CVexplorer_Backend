using CVexplorer.Models.Domain;
using CVexplorer.Models.DTO;
using Microsoft.AspNetCore.Mvc;

namespace CVexplorer.Repositories.Interface
{
    public interface IRoundRepository
    {
        Task<Round> CreateAsync(Guid positionId);

        Task<IEnumerable<RoundListDTO>> ListAsync(int? departmentId = null, string? publicPositionId = null);

        Task DeleteAsync(string publicId);

        Task<ActionResult<List<RoundStageDTO>>> GetAsync(string publicId);


    }
}
