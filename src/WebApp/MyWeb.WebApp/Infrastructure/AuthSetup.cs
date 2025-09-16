using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyWeb.Infrastructure.Data.Identity;

namespace MyWeb.WebApp.Infrastructure;

public static class AuthSetup
{
    public static IServiceCollection AddMyWebAuth(this IServiceCollection services, IConfiguration cfg)
    {
        // Identity DbContext (aynı MyWeb DB, auth şeması)
        services.AddDbContext<IdentityDb>(opt =>
        {
            opt.UseSqlServer(
                cfg.GetConnectionString("CatalogDb") ?? cfg.GetConnectionString("HistorianDb")!,
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", IdentityDb.Schema));
        });

        // ASP.NET Core Identity (cookie bazlı)
        services
            .AddIdentityCore<ApplicationUser>(o =>
            {
                o.Password.RequireDigit = false;
                o.Password.RequireNonAlphanumeric = false;
                o.Password.RequireUppercase = false;
                o.Password.RequiredLength = 6;
                o.User.RequireUniqueEmail = true;
                o.SignIn.RequireConfirmedAccount = false;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<IdentityDb>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        // Authentication (Identity.Application cookie)
        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
                options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
            })
            .AddCookie(IdentityConstants.ApplicationScheme, o =>
            {
                o.LoginPath = "/account/login";
                o.LogoutPath = "/account/logout";
                o.AccessDeniedPath = "/account/denied";
                o.Cookie.Name = ".MyWeb.Auth";
                o.Cookie.HttpOnly = true;
                o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                o.SlidingExpiration = true;
                o.ExpireTimeSpan = TimeSpan.FromHours(8);
            });

        // Authorization + Policy
        services.AddAuthorization(options =>
        {
            // Aşama 1: basit RBAC; Admin/Operator → Historian API erişimi
            options.AddPolicy("CanUseHistorian", p =>
                p.RequireRole("Admin", "Operator"));
        });

        return services;
    }
}
