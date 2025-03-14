using CVexplorer.Models.DTO;

namespace CVexplorer.Repositories.Interface.Admin
{
    public interface ICompanyManagementRepository
    {
        Task<List<CompanyManagementListDTO>> GetCompaniesAsync();
        Task<CompanyManagementDTO> UpdateCompanyAsync(int companyId, CompanyManagementDTO dto);
        Task<bool> DeleteCompanyAsync(int companyId);
        
        Task<CompanyManagementDTO> CreateCompanyAsync(CompanyManagementDTO dto);
    }
}
