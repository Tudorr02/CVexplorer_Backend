using Microsoft.AspNetCore.Identity;

namespace CVexplorer.Models.Domain
{
    public class Role : IdentityRole<int>
    {
        public ICollection<UserRole> UserRoles { get; set; } = [];
    }
}
