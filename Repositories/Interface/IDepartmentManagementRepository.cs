using CVexplorer.Models.DTO;

namespace CVexplorer.Repositories.Interface
{
    public interface IDepartmentManagementRepository
    {
        Task<List<DepartmentManagementDTO>> GetDepartmentsAsync(string companyName, int userId , bool hrLeader);
        Task<DepartmentManagementDTO?> GetDepartmentAsync(string companyName, string departmentName); // ✅ Fetch department by name & company
        Task<DepartmentManagementDTO?> CreateDepartmentAsync(string companyName, string departmentName);
        Task<DepartmentManagementDTO?> UpdateDepartmentAsync(string companyName, string departmentName, DepartmentManagementDTO dto);
        Task<bool> DeleteDepartmentAsync(string companyName, string departmentName);
    }
}
