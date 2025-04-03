using CVexplorer.Models.Domain;
using CVexplorer.Models.DTO;

namespace CVexplorer.Repositories.Interface
{
    public interface ICVRepository
    {
        Task<IEnumerable<CvListDTO>> GetAllCVsAsync(string publicPositionId);

        Task<bool> UploadDocumentAsync(IFormFile file, string publicPositionId, int userId);
        Task<bool> UploadArchiveAsync(IFormFile archiveFile, string publicPositionId, int userId);

        Task<CvDTO> GetCVAsync(Guid publicId);
    }
}

