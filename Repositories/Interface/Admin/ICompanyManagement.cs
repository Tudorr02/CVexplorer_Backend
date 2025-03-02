using CVexplorer.Models.DTO.Admin;

namespace CVexplorer.Repositories.Interface.Admin
{
    public interface ICompanyManagement
    {
        Task<List<CompanyManagementDTO>> GetCompaniesAsync();
        Task<CompanyManagementDTO> GetCompanyAsync(string companyName);
        Task<CompanyManagementDTO> UpdateCompanyAsync(string companyName, CompanyManagementDTO dto);
        Task<bool> DeleteCompanyAsync(string companyName);
        
        Task<CompanyManagementDTO> CreateCompanyAsync(CompanyManagementDTO dto);
    }
}
