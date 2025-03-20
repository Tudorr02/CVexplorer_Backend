using CVexplorer.Models.DTO;

namespace CVexplorer.Repositories.Interface
{
    public interface IDepartmentRepository
    {
        Task<List<DepartmentListDTO>> GetDepartmentsAsync(int companyId, int userId,bool isHrLeader = false);

        Task<DepartmentDTO?> GetDepartmentAsync(int departmentId, int companyId);

        Task<DepartmentListDTO> CreateDepartmentAsync(int companyId, DepartmentDTO dto);
        Task<DepartmentDTO> UpdateDepartmentAsync(int departmentID, int companyId ,DepartmentDTO dto);
        Task<bool> DeleteDepartmentAsync(int departmentId);

        Task<List<DepartmentTreeNodeDTO>> GetDepartmentsTreeAsync(int companyId, int userId, bool isHrLeader = false);

        Task<List<DepartmentAccessDTO>> GetDepartmentAccessAsync(int companyId);
        Task<bool> UpdateDepartmentAccessAsync(int departmentId, int companyId, List<int> userIds);
    }
}
 