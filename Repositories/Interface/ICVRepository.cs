using CVexplorer.Models.Domain;
using CVexplorer.Models.DTO;

namespace CVexplorer.Repositories.Interface
{
    public interface ICVRepository
    {
        Task<IEnumerable<CvListDTO>> GetAllCVsAsync(string? publicPositionId = null , int? departmentId =null);

        Task<bool> UploadDocumentAsync(IFormFile file, string publicPositionId, int userId , int? roundId =null);
        Task<bool> UploadArchiveAsync(IFormFile archiveFile, string publicPositionId, int userId);
        Task<bool> UploadBulkArchiveAsync(IFormFile archiveFile,
                                           string positionPublicId,
                                           int userId);

        Task<CvDTO> GetCVAsync(Guid publicId);


    }
}

