using CVexplorer.Data;
using CVexplorer.Exceptions;
using CVexplorer.Models.DTO;
using CVexplorer.Repositories.Interface;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace CVexplorer.Repositories.Implementation
{
    public class UserDetailsRepository : IUserDetailsRepository
    {
        private readonly DataContext _context;
        public UserDetailsRepository(DataContext context)
        {
            _context = context;
        }
        public async Task<UserDetailsDTO> GetUserDetailsAsync(int userId)
        {
            return await _context.Users
                .Where(u => u.Id == userId)
                .Select(u => new UserDetailsDTO
                {
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    CompanyName = u.CompanyName,
                    Email = u.Email
                })
                .FirstOrDefaultAsync();
        }
        public async Task<bool> UpdateUserDetailsAsync(int userId, UserDetailsDTO userDetails)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (user == null)
                {
                    throw new NotFoundException("User not found");
                }

                bool isUpdated = false;

                if (!string.IsNullOrWhiteSpace(userDetails.FirstName) && !userDetails.FirstName.Equals(user.FirstName))
                {
                    user.FirstName = userDetails.FirstName;
                    isUpdated = true;
                }

                if (!string.IsNullOrWhiteSpace(userDetails.LastName) && !userDetails.LastName.Equals(user.LastName))
                {
                    user.LastName = userDetails.LastName;
                    isUpdated = true;
                }

                if (!string.IsNullOrWhiteSpace(userDetails.Email) && !userDetails.Email.Equals(user.Email))
                {
                    user.Email = userDetails.Email;
                    isUpdated = true;
                }

                if (!isUpdated)
                {
                    throw new ValidationException("No changes detected.");
                }

                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                return false;
            }
        }
    }
}
