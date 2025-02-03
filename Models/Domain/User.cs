using Microsoft.AspNetCore.Identity;

namespace CVexplorer.Models.Domain
{
    public class User : IdentityUser<int>
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? CompanyName { get; set; }

        public ICollection<UserRole> UserRoles { get; set; } = [];
    }
}
