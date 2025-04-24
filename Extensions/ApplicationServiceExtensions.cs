using CVexplorer.Data;
using CVexplorer.Repositories.Implementation;
using CVexplorer.Repositories.Implementation.Admin;
using CVexplorer.Repositories.Interface;
using CVexplorer.Repositories.Interface.Admin;
using CVexplorer.Services.Implementation;
using CVexplorer.Services.Interface;
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
                opt.UseSqlServer(configuration.GetConnectionString("LocalConnection"));
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
            services.AddHttpClient<ICvEvaluationService, CvEvaluationService>(c =>
             {
                 var pythonBaseURL = "http://127.0.0.1:8000";
                 c.BaseAddress = new Uri(pythonBaseURL);
                 c.Timeout = TimeSpan.FromSeconds(30);
             });
            services.AddScoped<ICVRepository, CVRepository>();
            services.AddScoped<ICVEvaluationRepository, CVEvaluationRepository>();
            services.AddScoped<IRoundRepository, RoundRepository>();
            services.AddScoped<IRoundEntryRepository, RoundEntryRepository>();

            return services;
        }
    }
}
