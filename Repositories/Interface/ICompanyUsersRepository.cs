using CVexplorer.Models.DTO;
using CVexplorer.Models.DTO.Admin;

namespace CVexplorer.Repositories.Interface
{
    public interface ICompanyUsersRepository
    {
        Task<List<CompanyUsersDTO>> GetCompanyUsers(string companyName);
    }
}
