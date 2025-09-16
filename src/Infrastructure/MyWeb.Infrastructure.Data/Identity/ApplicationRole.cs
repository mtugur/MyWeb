using Microsoft.AspNetCore.Identity;

namespace MyWeb.Infrastructure.Data.Identity;

public class ApplicationRole : IdentityRole
{
    public ApplicationRole() : base() { }
    public ApplicationRole(string roleName) : base(roleName) { }
}
