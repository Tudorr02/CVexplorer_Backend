using AutoMapper;
using CVexplorer.Data;
using CVexplorer.Exceptions;
using CVexplorer.Models.Domain;
using CVexplorer.Models.DTO;
using CVexplorer.Models.DTO.Admin;
using CVexplorer.Repositories.Interface;
using CVexplorer.Repositories.Interface.Admin;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CVexplorer.Repositories.Implementation.Admin
{
    public class UserManagementRepository (UserManager<User> _userManager , DataContext _context , IMapper _mapper) : IUserManagement
    {
        public async Task<List<UserManagementDTO>> GetUsersAsync()
        {
            //var user = await _userManager.Users
            //    .Include(u => u.Company) // ✅ Ensure the company is loaded
            //    .FirstOrDefaultAsync(u => u.UserName == username);

            var users = await _userManager.Users
                                        .Include(u => u.Company)
                                        .ToListAsync();

            return users.Select(u => new UserManagementDTO
            {
                Username = u.UserName,
                FirstName = u.FirstName,
                LastName = u.LastName,
                CompanyName = _context.Companies.Where(c=> c.Id == u.CompanyId).Select(c=> c.Name)?.FirstOrDefault(),
                Email = u.Email,
                UserRoles = _userManager.GetRolesAsync(u).Result.ToList()
            }).ToList();
        }

        public async Task<UserManagementDTO> UpdateUserAsync (string username,UserManagementDTO dto)
        {
            var user = await _userManager.Users
                            .Include(u => u.UserRoles)
                            .FirstOrDefaultAsync(u => u.UserName == username);


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

            // ✅ Update Company (if provided)
            if (!string.IsNullOrWhiteSpace(dto.CompanyName) &&
                (user.Company == null || !dto.CompanyName.Equals(user.Company.Name)))
            {
                var company = await _context.Companies.FirstOrDefaultAsync(c => c.Name == dto.CompanyName);

                if (company == null)
                {
                    throw new NotFoundException($"Company '{dto.CompanyName}' not found");
                }

                user.CompanyId = company.Id;
                hasChanges = true;
            }

            // ✅ Update User Roles (if provided)
            if (dto.UserRoles.Count != user.UserRoles.Count)
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

            // ✅ Update user only if changes were made
            if (hasChanges)
            {
                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    throw new Exception("Failed to update user details.");
                }
            }

            return new UserManagementDTO
            {
                Username = user.UserName,
                FirstName = user.FirstName,
                LastName = user.LastName,
                CompanyName = user.Company?.Name,
                Email = user.Email,
                UserRoles = _userManager.GetRolesAsync(user).Result.ToList()
            };
        }

        public async Task<UserManagementDTO> GetUserAsync(string username)
        {
            //var user = await _userManager.Users
            //    .Include(u => u.Company) // ✅ Ensure the company is loaded
            //    .FirstOrDefaultAsync(u => u.UserName == username);

            var user = await _userManager.FindByNameAsync(username);

            if (user == null)
            {
                throw new NotFoundException("User not found !");
            }

            return new UserManagementDTO
            {
                Username = user.UserName,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                CompanyName = user.Company?.Name, // ✅ Avoids null reference if no company
                UserRoles = _userManager.GetRolesAsync(user).Result.ToList() // ✅ Fetch user roles
            };
        }

        public async Task<UserManagementDTO> DeleteUserAsync(string username)
        {
            var user = await _userManager.FindByNameAsync(username);

            if (user == null)
            {
                throw new NotFoundException("User not found");
            }

            var result = await _userManager.DeleteAsync(user);

            if (!result.Succeeded)
            {
                throw new Exception("Failed to delete user");
            }

            return new UserManagementDTO
            {
                Username = user.UserName,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                CompanyName = user.Company?.Name,
                UserRoles = _userManager.GetRolesAsync(user).Result.ToList()
            };
        }

        

    }
}
