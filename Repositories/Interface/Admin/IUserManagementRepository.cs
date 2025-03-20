using CVexplorer.Models.DTO;
using Microsoft.AspNetCore.Mvc;

namespace CVexplorer.Repositories.Interface.Admin
{
    public interface IUserManagementRepository
    {
        Task<List<UserManagementListDTO>> GetUsersAsync();
        Task<UserManagementDTO> UpdateUserAsync(int userId ,UserManagementDTO dto);
        Task<UserManagementDTO> DeleteUserAsync(int userId);
        Task<AccountDTO> EnrollUserAsync(UserEnrollDTO dto);
       
    }
}
