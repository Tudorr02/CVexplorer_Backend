using CVexplorer.Models.DTO;


namespace CVexplorer.Repositories.Interface
{
    public interface IUserRepository
    {
        Task<List<UserListDTO>> GetUsersAsync(int companyId);

        Task<UserDTO> UpdateUserAsync(int userId, UserDTO dto);

        Task<bool> DeleteUserAsync(int userId);
    }
}
