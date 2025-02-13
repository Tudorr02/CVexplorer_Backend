using CVexplorer.Models.DTO;

namespace CVexplorer.Repositories.Interface
{
    public interface IUserDetailsRepository
    {
        Task<UserDetailsDTO> GetUserDetailsAsync(int userId);
        Task<bool> UpdateUserDetailsAsync(int userId, UserDetailsDTO userDetails);
    }
}
