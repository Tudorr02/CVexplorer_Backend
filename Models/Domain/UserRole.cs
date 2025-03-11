using Microsoft.AspNetCore.Identity;

namespace CVexplorer.Models.Domain
{
    public class UserRole : IdentityUserRole<int>
    {
        public required User User { get; set; }
        public required Role Role { get; set; }
    }
}
