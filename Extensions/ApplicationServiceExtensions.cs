using CVexplorer.Data;
using CVexplorer.Repositories.Implementation;
using CVexplorer.Repositories.Implementation.Admin;
using CVexplorer.Repositories.Interface;
using CVexplorer.Repositories.Interface.Admin;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

namespace CVexplorer.Extensions
{
    public static class ApplicationServiceExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services , IConfiguration configuration)
        {
            services.AddControllers().AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });
            services.AddDbContext<DataContext>(opt =>
            {
                opt.UseSqlServer(configuration.GetConnectionString("LocalConnectionPC"));
            });
            
            services.AddCors();
            services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
            services.AddScoped<ITokenService, TokenService>();
            services.AddScoped<IUserDetailsRepository, UserDetailsRepository>();
            services.AddScoped<IUserManagementRepository, UserManagementRepository>();
            services.AddScoped<ICompanyManagementRepository, CompanyManagementRepository>();
            services.AddScoped<IDepartmentRepository,DepartmentRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IPositionRepository, PositionRepository>();
            services.AddScoped<ICVRepository, CVRepository>();



            return services;
        }
    }
}
