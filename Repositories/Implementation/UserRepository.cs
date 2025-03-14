using CVexplorer.Data;
using CVexplorer.Exceptions;
using CVexplorer.Models.Domain;
using CVexplorer.Models.DTO;
using CVexplorer.Repositories.Interface;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CVexplorer.Repositories.Implementation
{
    public class UserRepository(DataContext _context, UserManager<User> _userManager) : IUserRepository
    {
        public async Task<List<UserListDTO>> GetUsersAsync(int companyId)
        {
            
            var users = await _context.Users
                .Where(u => u.CompanyId == companyId) 
                .ToListAsync();

            return users  // ✅ Use `companyId` instead of `companyName`
               .Select(u => new UserListDTO
               {
                   Id = u.Id,
                   Username = u.UserName,
                   FirstName = u.FirstName,
                   LastName = u.LastName,
                   Email = u.Email,
                   UserRoles = _userManager.GetRolesAsync(u).Result.ToList()
               })
                .ToList();
        }

        public async Task<UserDTO> UpdateUserAsync(int userId, UserDTO dto)
        {
            var user = await _userManager.Users
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                throw new NotFoundException("User not found");
            }

            bool hasChanges = false; // ✅ Track if any update was made

            // ✅ Update FirstName
            if (!string.IsNullOrWhiteSpace(dto.FirstName) && !dto.FirstName.Equals(user.FirstName))
            {
                user.FirstName = dto.FirstName;
                hasChanges = true;
            }

            // ✅ Update LastName
            if (!string.IsNullOrWhiteSpace(dto.LastName) && !dto.LastName.Equals(user.LastName))
            {
                user.LastName = dto.LastName;
                hasChanges = true;
            }

            // ✅ Update Email
            if (!string.IsNullOrWhiteSpace(dto.Email) && !dto.Email.Equals(user.Email))
            {
                user.Email = dto.Email;
                hasChanges = true;
            }

            // ✅ Update User Roles (if provided)
            if (dto.UserRoles != null)
            {
                var currentRoles = await _userManager.GetRolesAsync(user);
                var rolesToRemove = currentRoles.Except(dto.UserRoles).ToList();
                var rolesToAdd = dto.UserRoles.Except(currentRoles).ToList();

                // ✅ Validate roles exist before updating
                var validRoles = await _context.Roles.Select(r => r.Name).ToListAsync();
                var invalidRoles = rolesToAdd.Except(validRoles).ToList();

                if (invalidRoles.Any())
                {
                    throw new Exception($"Invalid roles: {string.Join(", ", invalidRoles)}");
                }

                // ✅ Remove roles if necessary
                if (rolesToRemove.Any())
                {
                    var removeResult = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
                    if (!removeResult.Succeeded)
                    {
                        throw new Exception("Failed to remove existing roles.");
                    }
                    hasChanges = true; // ✅ Track role removal as a change

                }

                // ✅ Add new roles
                if (rolesToAdd.Any())
                {
                    var addResult = await _userManager.AddToRolesAsync(user, rolesToAdd);
                    if (!addResult.Succeeded)
                    {
                        throw new Exception("Failed to assign new roles.");
                    }
                    hasChanges = true; // ✅ Track role addition as a change

                }
            }

            if (hasChanges)
            {
                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    throw new Exception("Failed to update user.");
                }
            }

            return new UserDTO
            {
                
                Username = user.UserName,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                UserRoles = _userManager.GetRolesAsync(user).Result.ToList()
            };
        }


    }
}
