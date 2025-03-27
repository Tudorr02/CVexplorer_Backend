using CVexplorer.Models.DTO;

namespace CVexplorer.Repositories.Interface
{
    public interface IPositionRepository
    {
        
        Task<PositionDTO> GetPositionAsync(string publicId);
        Task<PositionListDTO> CreatePositionAsync(int departmentId,PositionDTO dto);
        Task<bool> UpdatePositionAsync(string publicId, PositionDTO dto);
        Task<bool> DeletePositionAsync(string publicId);
    }
}

