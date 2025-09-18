using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MyWeb.WebApp.Auth;
using MyWeb.WebApp.Authorization;
using MyWeb.WebApp.Middleware;
using MyWeb.WebApp.Startup;

namespace MyWeb.WebApp.Auth
{
    public static class Stage2AuthExtensions
    {
        public static IServiceCollection AddStage2Auth(this IServiceCollection services, IConfiguration config)
        {
            services.Configure<JwtOptions>(config.GetSection("Jwt"));

            services.AddScoped<JwtTokenService>();
            // Değişiklik: Permissive yerine DB tabanlı servis
            services.AddScoped<ITagPermissionService, DbTagPermissionService>();

            services.Configure<MvcOptions>(opts => { opts.Filters.Add<HistPermissionFilter>(); });

            services.AddAuthentication()
                    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, opts =>
                    {
                        var jwt = config.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
                        opts.RequireHttpsMetadata = true;
                        opts.SaveToken = false;
                        opts.TokenValidationParameters = new TokenValidationParameters
                        {
                            ValidIssuer = jwt.Issuer,
                            ValidAudience = jwt.Audience,
                            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                            ValidateIssuerSigningKey = true,
                            ValidateIssuer = true,
                            ValidateAudience = true,
                            ValidateLifetime = true,
                            ClockSkew = System.TimeSpan.Zero
                        };
                    });

            services.AddAuthorization(options =>
            {
                if (options.GetPolicy("CanUseHistorian") is null)
                    options.AddPolicy("CanUseHistorian", p => p.RequireAuthenticatedUser());
            });

            services.AddHostedService<AuthSeederHostedService>();
            return services;
        }

        public static IApplicationBuilder UseStage2Auth(this IApplicationBuilder app)
        {
            app.UseMiddleware<ApiJwtEnforcerMiddleware>();
            return app;
        }
    }
}
