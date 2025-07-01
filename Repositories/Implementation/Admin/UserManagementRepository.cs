using AutoMapper;
using CVexplorer.Data;
using CVexplorer.Exceptions;
using CVexplorer.Models.Domain;
using CVexplorer.Models.DTO;
using CVexplorer.Repositories.Interface;
using CVexplorer.Repositories.Interface.Admin;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CVexplorer.Repositories.Implementation.Admin
{
    public class UserManagementRepository (UserManager<User> _userManager , DataContext _context , IMapper _mapper , ITokenService _tokenService) : IUserManagementRepository
    {
        public async Task<List<UserManagementListDTO>> GetUsersAsync()
        {
            
            var users = await _userManager.Users
                                        .Include(u => u.Company)
                                        .Include(u => u.UserRoles)
                                        .ThenInclude(ur => ur.Role)
                                        .ToListAsync();

            return users.Select(u => new UserManagementListDTO
            {
                Id = u.Id,
                Username = u.UserName,
                FirstName = u.FirstName,
                LastName = u.LastName,
                CompanyName = u.Company?.Name,
                Email = u.Email,
                UserRole = u.UserRoles.FirstOrDefault().Role?.Name
            }).ToList();
        }

        public async Task<UserManagementDTO> UpdateUserAsync (int userId,UserManagementDTO dto)
        {
            var user = await _userManager.Users
                            .Include(u => u.UserRoles)
                            .ThenInclude(ur => ur.Role)
                            .Include(u => u.Company)
                            .FirstOrDefaultAsync(u => u.Id == userId);


            if (user == null)
            {
                throw new NotFoundException("User not found");
            }

            bool hasChanges = false;

            if (!string.IsNullOrWhiteSpace(dto.FirstName) && !dto.FirstName.Equals(user.FirstName))
            {
                user.FirstName = dto.FirstName;
                hasChanges = true;
            }

            if (!string.IsNullOrWhiteSpace(dto.LastName) && !dto.LastName.Equals(user.LastName))
            {
                user.LastName = dto.LastName;
                hasChanges = true;
            }

            if (!string.IsNullOrWhiteSpace(dto.Email) && !dto.Email.Equals(user.Email))
            {
                user.Email = dto.Email;
                hasChanges = true;
            }

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

            if (!string.IsNullOrWhiteSpace(dto.UserRole))
            {
                var currentRole = user.UserRoles.FirstOrDefault()?.Role?.Name;

                if(currentRole !=null)
                {
                    if (!currentRole.ToLower().Equals(dto.UserRole.ToLower()))
                    {
                        user.UserRoles.Clear();

                        var validRoles = await _context.Roles.Select(r => r.Name).ToListAsync();
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
                UserRole = user.UserRoles.FirstOrDefault()?.Role?.Name
            };
        }

        public async Task<UserManagementDTO> DeleteUserAsync(int userId)
        {
            
            var user = await _userManager.Users
                            .Include(u => u.UserRoles)
                            .ThenInclude(ur => ur.Role)
                            .Include(u => u.Company)
                            .FirstOrDefaultAsync(u => u.Id == userId);

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
                UserRole = user.UserRoles.FirstOrDefault()?.Role?.Name
            };
        }

        public async Task<AccountDTO> EnrollUserAsync(UserEnrollDTO dto)
        {
            if (await UserExists(dto.Username.ToLower()))
                throw new ArgumentException("Username is taken");

            int? companyId = null;
            if (!string.IsNullOrWhiteSpace(dto.CompanyName))
            {
                var company = await _context.Companies.FirstOrDefaultAsync(c => c.Name == dto.CompanyName);
                if (company == null)
                    throw new ArgumentException($"Company '{dto.CompanyName}' not found");

                companyId = company.Id;
            }

            var roleToAssign = string.IsNullOrWhiteSpace(dto.UserRole) ? "HRUser" : dto.UserRole;
            var validRoles = await _context.Roles.Select(r => r.Name).ToListAsync();
            if (!validRoles.Contains(roleToAssign))
                throw new ArgumentException($"Invalid role: {roleToAssign}");

            var user = _mapper.Map<User>(dto);
            user.UserName = dto.Username;
            user.CompanyId = companyId;

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
                throw new InvalidOperationException($"Failed to register: {string.Join(", ", result.Errors.Select(e => e.Description))}");

            var addRolesResult = await _userManager.AddToRoleAsync(user, roleToAssign);
            if (!addRolesResult.Succeeded)
                throw new InvalidOperationException("Failed to assign roles.");

            return new AccountDTO
            {
                Username = user.UserName,
                Token = await _tokenService.CreateToken(user),
            };
        }

        private async Task<bool> UserExists(string username)
        {
            return await _userManager.Users.AnyAsync(x => x.NormalizedUserName.ToLower() == username.ToLower());
        }



    }
}
