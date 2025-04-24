using CVexplorer.Models.Domain;

namespace CVexplorer.Repositories.Interface
{
    public interface IRoundRepository
    {
        Task<Round> CreateAsync(Guid positionId);

        Task<IEnumerable<Round>> ListAsync(int? departmentId = null, string? publicPositionId = null);

        Task DeleteAsync(string publicId);


    }
}
