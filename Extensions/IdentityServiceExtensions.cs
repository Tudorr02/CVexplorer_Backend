using CVexplorer.Data;
using CVexplorer.Models.Domain;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using ProjectName.Data.Seeders;
using System.Text;

namespace CVexplorer.Extensions
{
    public static class IdentityServiceExtensions
    {
        public static IServiceCollection AddIdentityService(this IServiceCollection services, IConfiguration configuration   )
        {

            services.AddIdentityCore<User>(opt =>
            {
                opt.Password.RequireNonAlphanumeric = false;
                //opt.Password.RequiredLength = 3;             // Set the minimum password length
                opt.Password.RequireUppercase = false;       // Optional: Disable uppercase requirement
                opt.Password.RequireDigit = false;

            })
                .AddRoles<Role>()
                .AddRoleManager<RoleManager<Role>>() 
                .AddEntityFrameworkStores<DataContext>();


            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
             .AddJwtBearer(options =>
             {
                 var tokenKey = configuration["TokenKey"] ?? throw new Exception("TokenKey not found");
                 options.TokenValidationParameters = new TokenValidationParameters
                 {
                     ValidateIssuerSigningKey = true,
                     IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tokenKey)),
                     ValidateIssuer = false,
                     ValidateAudience = false
                 };
             });


            // Seed roles using RoleSeeder
            using (var scope = services.BuildServiceProvider().CreateScope())
            {
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
                RoleSeeder.SeedRoles(roleManager).Wait(); // Wait ensures seeding is done synchronously
            }

            services.AddAuthorizationBuilder()
                .AddPolicy("RequireAdminRole", policy => policy.RequireRole("Admin"))
                .AddPolicy("RequireModeratorRole", policy => policy.RequireRole("Admin", "Moderator"))
                .AddPolicy("RequireUserRole", policy => policy.RequireRole("Admin", "Moderator", "User"));

            return services;
        }
    }
}
