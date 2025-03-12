using CVexplorer.Data;
using CVexplorer.Exceptions;
using CVexplorer.Models.Domain;
using CVexplorer.Models.DTO;
using CVexplorer.Repositories.Interface;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CVexplorer.Repositories.Implementation
{
    public class CompanyUserRepository(DataContext _context, UserManager<User> _userManager) : ICompanyUserRepository
    {
        public async Task<List<CompanyUserDTO>> GetUsersByCompanyAsync(string companyName)
        {
            // Check if the company exists
            var company = await _context.Companies
                .FirstOrDefaultAsync(c => c.Name.ToLower() == companyName.ToLower());

            if (company == null)
            {
                throw new NotFoundException($"Company '{companyName}' not found.");
            }

            // Fetch users associated with the company
            var users = await _context.Users
                .Where(u => u.CompanyId == company.Id)
                .ToListAsync();

            // Convert to DTO
            var userDtos = new List<CompanyUserDTO>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user); // Get user roles
                userDtos.Add(new CompanyUserDTO
                {
                    Username = user.UserName,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email,
                    UserRoles = roles.ToList()
                });
            }

            return userDtos;
        }

    }
}
