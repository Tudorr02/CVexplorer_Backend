using CVexplorer.Data;
using CVexplorer.Exceptions;
using CVexplorer.Models.Domain;
using CVexplorer.Models.DTO;
using CVexplorer.Repositories.Interface;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace CVexplorer.Repositories.Implementation
{
    public class UserRepository(DataContext _context, UserManager<User> _userManager) : IUserRepository
    {
        public async Task<List<UserListDTO>> GetUsersAsync(int companyId)
        {
            
            var users = await _context.Users
                .Where(u => u.CompanyId == companyId) 
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .ToListAsync();

            return users  // ✅ Use `companyId` instead of `companyName`
               .Select(u => new UserListDTO
               {
                   Id = u.Id,
                   Username = u.UserName,
                   FirstName = u.FirstName,
                   LastName = u.LastName,
                   Email = u.Email,
                   UserRole = u.UserRoles.FirstOrDefault().Role?.Name
               })
                .ToList();
        }

        public async Task<UserDTO> UpdateUserAsync(int userId, UserDTO dto)
        {
            var user = await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
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

            if (!string.IsNullOrWhiteSpace(dto.UserRole))
            {
                var currentRole = user.UserRoles.FirstOrDefault()?.Role?.Name;

                if (currentRole != null)
                {
                    if (!currentRole.ToLower().Equals(dto.UserRole.ToLower()))
                    {
                        user.UserRoles.Clear();

                        // ✅ Validate the new role exists
                        var validRoles = new List<string> { "HRLeader", "HRUser" };
                        if (!validRoles.Contains(dto.UserRole))
                        {
                            throw new Exception($"Invalid role: {dto.UserRole}");
                        }

                        var addResult = await _userManager.AddToRoleAsync(user, dto.UserRole);
                        if (!addResult.Succeeded)
                        {
                            throw new Exception("Failed to assign new role.");
                        }
                        hasChanges = true;

                    }

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
                UserRole = user.UserRoles.FirstOrDefault().Role?.Name,
            };
        }

        public async Task<bool> DeleteUserAsync(int userId)
        {
            var user = await _context.Users
                           .Include(u => u.UserRoles)
                           .ThenInclude(ur => ur.Role)
                           .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                throw new NotFoundException("User not found.");
            }

            // ✅ Prevent deletion of Admins or Moderators
            var userRole = user.UserRoles.FirstOrDefault().Role?.Name;

            if (userRole.Equals("Admin") || userRole.Equals("Moderator"))
                throw new UnauthorizedAccessException("You are not allowed to delete Admin or Moderator users.");
            

            var result = await _userManager.DeleteAsync(user);

            if (!result.Succeeded)
            {
                throw new Exception("Failed to delete user.");
            }

            return true;
        }

        public async Task<bool> EnrollUserAsync(int companyId, UserEnrollDTO dto)
        {
            if (await _userManager.FindByNameAsync(dto.Username.ToLower()) != null)
                throw new ValidationException("Username is already taken.");
            

            // ✅ Validate roles before creating user
            var roleToAssign = string.IsNullOrWhiteSpace(dto.UserRole) ? "HRUser" : dto.UserRole;
            var validRoles = await _context.Roles.Select(r => r.Name).ToListAsync();
            if (!validRoles.Contains(roleToAssign))
                throw new ArgumentException($"Invalid role: {roleToAssign}");


            var newUser = new User
            {
                UserName = dto.Username,
                CompanyId = companyId
            };

            var result = await _userManager.CreateAsync(newUser, dto.Password);
            if (!result.Succeeded)
            {
                throw new ValidationException($"User creation failed: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }

            var addRolesResult = await _userManager.AddToRoleAsync(newUser, roleToAssign);
            if (!addRolesResult.Succeeded)
                throw new ValidationException("Failed to assign roles.");

            return true;
        }

    }
}
