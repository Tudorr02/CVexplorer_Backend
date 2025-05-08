using CVexplorer.Data;
using CVexplorer.Models.Domain;
using Google.Apis.Gmail.v1;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
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
                opt.Password.RequiredLength = 5;             // Set the minimum password length
                opt.Password.RequireUppercase = false;       // Optional: Disable uppercase requirement
                opt.Password.RequireDigit = false;

            })
                .AddRoles<Role>()
                .AddRoleManager<RoleManager<Role>>()
                .AddEntityFrameworkStores<DataContext>();


            //services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            // .AddJwtBearer(options =>
            // {
            //     var tokenKey = configuration["TokenKey"] ?? throw new Exception("TokenKey not found");
            //     options.TokenValidationParameters = new TokenValidationParameters
            //     {
            //         ValidateIssuerSigningKey = true,
            //         IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tokenKey)),
            //         ValidateIssuer = false,
            //         ValidateAudience = false,
            //         ValidateLifetime = true,
            //         RequireExpirationTime = true,


            //         ClockSkew = TimeSpan.Zero 

            //     };

            //     options.Events = new JwtBearerEvents
            //     {
            //         OnAuthenticationFailed = context =>
            //         {
            //             if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
            //             {
            //                 context.Response.Headers.Add("Token-Expired", "true"); 
            //                 context.Response.StatusCode = 401; 
            //                 context.Response.ContentType = "application/json";
            //                 return context.Response.WriteAsync("{\"error\": \"Token expired. Please log in again.\"}");
            //             }
            //             return Task.CompletedTask;
            //         }
            //     };
            // });

            services
                .AddAuthentication(options =>
                {
                    // Validate JWT for API requests
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                    // Use Cookie for external sign-ins (Google)
                    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;

                     
                })
                // JWT Bearer
                .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    var tokenKey = configuration["TokenKey"]
                                   ?? throw new Exception("TokenKey not found");
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tokenKey)),
                        ValidateIssuer = false,
                        ValidateAudience = false,
                        ValidateLifetime = true,
                        RequireExpirationTime = true,
                        ClockSkew = TimeSpan.Zero
                    };
                    options.Events = new JwtBearerEvents
                    {
                        // ❶ Încarcă token-ul din cookie "jwt" dacă există
                        OnMessageReceived = context =>
                        {
                            if (context.Request.Cookies.TryGetValue("jwt", out var jwt))
                            {
                                context.Token = jwt;
                            }
                            return Task.CompletedTask;
                        },
                        // ❷ Verifică dacă token-ul a expirat
                        OnAuthenticationFailed = context =>
                        {
                            if (context.Exception is SecurityTokenExpiredException)
                            {
                                context.Response.Headers.Add("Token-Expired", "true");
                                context.Response.StatusCode = 401;
                                return context.Response.WriteAsync(
                                    "{\"error\": \"Token expired. Please log in again.\"}"
                                );
                            }
                            return Task.CompletedTask;
                        }
                    };
                })
                // Cookie Scheme (for Google OAuth)
                .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
                {
                    options.Cookie.SameSite = SameSiteMode.None;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                })
                // Google OAuth2
                .AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
                {
                    options.ClientId = configuration["Google:ClientId"];
                    options.ClientSecret = configuration["Google:ClientSecret"];
                    //options.CallbackPath = "/api/Gmail/google-response";

                    options.Scope.Add(GmailService.Scope.GmailLabels);
                    options.Scope.Add(GmailService.Scope.GmailReadonly);

                    options.SaveTokens = true;
                    options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;

                    options.Events.OnRedirectToAuthorizationEndpoint = context =>
                    {
                        // the context.RedirectUri has all the standard parameters
                        var uri = context.RedirectUri
                                  + "&access_type=offline"
                                  + "&prompt=consent";
                        context.Response.Redirect(uri);
                        return Task.CompletedTask;
                    };

                    options.Events.OnTicketReceived = async ctx =>
                    {
                        var userManager = ctx.HttpContext.RequestServices
                        .GetRequiredService<UserManager<User>>();

                        if (!ctx.Properties.Items.TryGetValue("UserId", out var localUserId))
                            return; // n-ai pus UserId în props, deci nu faci nimic

                        var user = await userManager.FindByIdAsync(localUserId);
                        if (user == null) return;
                        
                        var refreshToken = ctx.Properties.GetTokenValue("refresh_token");
                        if (!string.IsNullOrEmpty(refreshToken))
                        {
                            await userManager.SetAuthenticationTokenAsync(
                                user,
                                GoogleDefaults.AuthenticationScheme, // provider
                                "refresh_token",                     // numele tokenului
                                refreshToken
                            );
                        }

                        // 2) Access token (opțional, dar util)
                        var accessToken = ctx.Properties.GetTokenValue("access_token");
                        if (!string.IsNullOrEmpty(accessToken))
                        {
                            await userManager.SetAuthenticationTokenAsync(
                                user,
                                GoogleDefaults.AuthenticationScheme,
                                "access_token",
                                accessToken
                            );
                        }

                        // 3) Expiration (pentru a şti când expiră access_token)
                        var expiresAt = ctx.Properties.GetTokenValue("expires_at");
                        if (!string.IsNullOrEmpty(expiresAt) && DateTimeOffset.TryParse(expiresAt, out var expiresAtDto))
                        {
                            var unixSeconds = expiresAtDto.ToUnixTimeSeconds().ToString();
                            await userManager.SetAuthenticationTokenAsync(
                                    user,
                                    GoogleDefaults.AuthenticationScheme,
                                    "expires_at",
                                    unixSeconds);
                        }

                 

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
                .AddPolicy("RequireHRLeaderRole", policy => policy.RequireRole("HRLeader"))
                .AddPolicy("RequireHRUserRole", policy => policy.RequireRole("HRLeader", "HRUser"))
                .AddPolicy("RequireAllRoles", policy => policy.RequireRole("Admin", "Moderator","HRLeader", "HRUser"));


            return services;
        }
    }
}
