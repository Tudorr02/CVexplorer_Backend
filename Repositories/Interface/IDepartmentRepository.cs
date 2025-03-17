using CVexplorer.Models.DTO;

namespace CVexplorer.Repositories.Interface
{
    public interface IDepartmentRepository
    {
        Task<List<DepartmentListDTO>> GetDepartmentsAsync(int companyId, int userId,bool isHrLeader = false);
        Task<DepartmentListDTO> CreateDepartmentAsync(int companyId, string departmentName);
        Task<DepartmentDTO?> UpdateDepartmentAsync(int departmentID, DepartmentDTO dto);
        Task<bool> DeleteDepartmentAsync(int departmentId);

        Task<List<DepartmentTreeNodeDTO>> GetDepartmentsTreeAsync(int companyId, int userId, bool isHrLeader = false);
    }
}
 