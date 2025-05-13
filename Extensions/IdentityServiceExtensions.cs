using CVexplorer.Data;
using CVexplorer.Models.Domain;
using Google.Apis.Gmail.v1;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using ProjectName.Data.Seeders;
using System.Security.Claims;
using System.Text;
using Microsoft.Identity.Web;

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





            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                })

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
                .AddCookie("GoogleCookie", opts =>
                {
                    opts.Cookie.Name = "Google.Auth";
                    opts.Cookie.SameSite = SameSiteMode.None;
                    opts.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    opts.ExpireTimeSpan = TimeSpan.FromDays(1);
                })
                .AddCookie("MicrosoftCookie", opts =>
                {
                    opts.Cookie.Name = "Microsoft.Auth";
                    opts.Cookie.SameSite = SameSiteMode.None;
                    opts.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    opts.ExpireTimeSpan = TimeSpan.FromDays(1);
                })
                .AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
                {
                    options.ClientId = configuration["Google:ClientId"];
                    options.ClientSecret = configuration["Google:ClientSecret"];

                    options.Scope.Add(GmailService.Scope.GmailLabels);
                    options.Scope.Add(GmailService.Scope.GmailReadonly);

                    options.SaveTokens = true;
                    options.SignInScheme = "GoogleCookie";

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
                            return; 

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

                        ctx.Properties.IsPersistent = true;

                        await ctx.HttpContext.SignInAsync(
                             "GoogleCookie",
                            ctx.Principal,
                            ctx.Properties
                            );

                        // 3) Redirectezi unde vrei
                        var redirectUri = ctx.ReturnUri ?? "/";
                        ctx.HttpContext.Response.Redirect(redirectUri);

                        // 4) Blochezi pipeline-ul implicit ca să nu fie dublă redirecționare
                        ctx.HandleResponse();

                    };
                })
                .AddOpenIdConnect("Microsoft",
                    options =>
                    {
                        options.Authority = $"{configuration["Microsoft:AzureAd:Instance"]}{configuration["Microsoft:AzureAd:TenantId"]}/v2.0";
                        options.ClientId = configuration["Microsoft:AzureAd:ClientId"];
                        options.ClientSecret = configuration["Microsoft:AzureAd:ClientSecret"];
                        options.CallbackPath = configuration["Microsoft:AzureAd:CallbackPath"];

                        options.TokenValidationParameters.ValidateIssuer = false;
                        // PKCE + code flow recomandat
                        options.ResponseType = OpenIdConnectResponseType.Code;
                        options.UsePkce = true;
                        options.SaveTokens = false;
                        options.SignInScheme = "MicrosoftCookie";

                        options.Scope.Clear();
                        options.Scope.Add("User.Read");
                        options.Scope.Add("Mail.Read");
                        options.Scope.Add("offline_access");
                        options.Scope.Add("openid");           //  ← obligatoriu
                        options.Scope.Add("profile");          //  ← util pt. nume/email

                        options.Scope.Add("Mail.Send");

                        options.Events = new OpenIdConnectEvents
                        {
                            OnTokenValidated = async ctx =>
                            {
                                if (!ctx.Properties.Items.TryGetValue("UserId", out var localUserId))
                                    return;

                                var userManager = ctx.HttpContext.RequestServices.GetRequiredService<UserManager<User>>();
                                ctx.Properties.IsPersistent = true;

                                var user = await userManager.FindByIdAsync(localUserId);
                                if (user == null) return;

                                // 2) salvează refresh / access token-ul exact ca la Google
                                var refreshToken = ctx.TokenEndpointResponse?.RefreshToken;
                                if (!string.IsNullOrEmpty(refreshToken))
                                {
                                    await userManager.SetAuthenticationTokenAsync(
                                        user, "Microsoft", "refresh_token", refreshToken);
                                }

                                var accessToken = ctx.TokenEndpointResponse?.AccessToken;
                                if (!string.IsNullOrEmpty(accessToken))
                                {
                                    await userManager.SetAuthenticationTokenAsync(
                                        user, "Microsoft", "access_token", accessToken);
                                }

                                var expiresIn = ctx.TokenEndpointResponse?.ExpiresIn;
                                if (!string.IsNullOrEmpty(expiresIn))
                                {
                                    var expiresAt = DateTimeOffset.UtcNow.AddSeconds(double.Parse(expiresIn))
                                                                         .ToUnixTimeSeconds().ToString();
                                    await userManager.SetAuthenticationTokenAsync(
                                        user, "Microsoft", "expires_at", expiresAt);
                                }
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
