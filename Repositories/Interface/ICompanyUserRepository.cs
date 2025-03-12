using CVexplorer.Models.DTO;
using CVexplorer.Models.DTO.Admin;

namespace CVexplorer.Repositories.Interface
{
    public interface ICompanyUserRepository
    {
        Task<List<CompanyUserDTO>> GetUsersByCompanyAsync(string companyName);
    }
}
