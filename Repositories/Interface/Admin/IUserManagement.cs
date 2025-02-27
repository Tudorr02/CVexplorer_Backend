using CVexplorer.Models.DTO;
using CVexplorer.Models.DTO.Admin;
using Microsoft.AspNetCore.Mvc;

namespace CVexplorer.Repositories.Interface.Admin
{
    public interface IUserManagement
    {
        Task<List<UserManagementDTO>> GetUsersAsync();
        Task<UserManagementDTO> GetUserAsync(string username);
        Task<UserManagementDTO> UpdateUserAsync(string username ,UserManagementDTO dto);
        Task<UserManagementDTO> DeleteUserAsync(string username);
    }
}
