using Microsoft.AspNetCore.Identity;

namespace MyWeb.Infrastructure.Data.Identity;

public class ApplicationUser : IdentityUser
{
    // İlk sürümde ek profil alanlarına gerek yok. Gerekirse burada genişletilir.
    public bool MustChangePassword { get; set; } = false;
}
