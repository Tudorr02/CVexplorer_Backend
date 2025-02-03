using CVexplorer.Models.Domain;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;

namespace ProjectName.Data.Seeders
{
    public static class RoleSeeder
    {
        public static async Task SeedRoles(RoleManager<Role> roleManager)
        {
            var roles = new[] { "Admin", "Moderator", "User" };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new Role() { Name = role });
                }
            }
        }
    }
}
