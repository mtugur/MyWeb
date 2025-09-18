using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyWeb.Infrastructure.Data.Identity;

namespace MyWeb.WebApp.Startup
{
    /// <summary>İdempotent rol/kullanıcı seed.</summary>
    public sealed class AuthSeederHostedService : IHostedService
    {
        private readonly IServiceProvider _sp;
        private readonly ILogger<AuthSeederHostedService> _log;

        public AuthSeederHostedService(IServiceProvider sp, ILogger<AuthSeederHostedService> log)
        {
            _sp = sp;
            _log = log;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var scope = _sp.CreateScope();

            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // --- Rolleri oluştur ---
            foreach (var roleName in new[] { "Admin", "Operator" })
            {
                if (!await roles.RoleExistsAsync(roleName))
                {
                    var res = await roles.CreateAsync(new ApplicationRole { Name = roleName });
                    if (!res.Succeeded)
                        _log.LogWarning("Role seed '{Role}' errors: {Errors}", roleName, string.Join(" | ", res.Errors.Select(e => e.Description)));
                }
            }

            // --- Admin kullanıcısı ---
            const string email = "admin@local";
            const string password = "MyWeb!2025";

            var admin = await users.FindByEmailAsync(email);
            if (admin is null)
            {
                admin = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
                var res = await users.CreateAsync(admin, password);
                if (!res.Succeeded)
                {
                    _log.LogError("Admin create failed: {Errors}", string.Join(" | ", res.Errors.Select(e => e.Description)));
                    return;
                }
            }

            if (!await users.IsInRoleAsync(admin, "Admin"))
                await users.AddToRoleAsync(admin, "Admin");

            _log.LogInformation("Auth seed completed (admin@local in Admin).");
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
