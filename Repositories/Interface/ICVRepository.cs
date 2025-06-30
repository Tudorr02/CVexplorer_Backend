using CVexplorer.Models.Domain;
using CVexplorer.Models.DTO;

namespace CVexplorer.Repositories.Interface
{
    public interface ICVRepository
    {
        Task<IEnumerable<CvListDTO>> GetAllCVsAsync(string? publicPositionId = null , int? departmentId =null);

        Task<bool> UploadDocumentAsync(IFormFile file, string publicPositionId, int userId , int? roundId =null, string? uploadMethod = "Manual");
        Task<bool> UploadBulkArchiveAsync(IFormFile archiveFile, string positionPublicId, int userId);

        Task<object> DeleteCVsAsync(List<Guid> cvPublicIds, string? positionPublicId = null, int? departmentId = null);
       

    }
}

