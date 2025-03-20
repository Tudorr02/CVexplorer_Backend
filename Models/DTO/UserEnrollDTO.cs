using System.ComponentModel.DataAnnotations;

namespace CVexplorer.Models.DTO
{
    public class UserEnrollDTO
    {
        [MaxLength(60)]
        public required string Username { get; set; }
        public required string Password { get; set; }

        public string? FirstName { get; set; }

        public string? LastName { get; set; }

        public string? CompanyName { get; set; }

        public string? Email { get; set; }

        public List<string> UserRoles { get; set; } = [];
    }
}
