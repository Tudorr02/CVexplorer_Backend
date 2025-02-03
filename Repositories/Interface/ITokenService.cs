using CVexplorer.Models.Domain;

namespace CVexplorer.Repositories.Interface
{
    public interface ITokenService
    {
        Task<string> CreateToken(User user);
    }
}
