using CVexplorer.Models.DTO;

namespace CVexplorer.Repositories.Interface
{
    public interface IDepartmentRepository
    {
        Task<List<DepartmentDTO>> GetDepartmentsAsync(string companyName, int userId , bool hrLeader);
        Task<DepartmentDTO?> GetDepartmentAsync(string companyName, string departmentName); // ✅ Fetch department by name & company
        Task<DepartmentDTO?> CreateDepartmentAsync(string companyName, string departmentName);
        Task<DepartmentDTO?> UpdateDepartmentAsync(string companyName, string departmentName, DepartmentDTO dto);
        Task<bool> DeleteDepartmentAsync(string companyName, string departmentName);
    }
}
