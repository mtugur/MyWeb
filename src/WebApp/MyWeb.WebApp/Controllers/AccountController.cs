using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MyWeb.Infrastructure.Data.Identity;

namespace MyWeb.WebApp.Controllers;

[Route("account")]
public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly UserManager<ApplicationUser> _users;

    public AccountController(SignInManager<ApplicationUser> signIn, UserManager<ApplicationUser> users)
    {
        _signIn = signIn;
        _users = users;
    }

    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View(); // İstersen şimdilik boş bir Razor View ile basit form ekleriz; yoksa API post yolu da kullanabiliriz.
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginPost([FromForm] string email, [FromForm] string password, string? returnUrl = null)
    {
        var user = await _users.FindByEmailAsync(email);
        if (user is null)
        {
            await Task.Delay(300); // timing attack azaltma
            return Unauthorized("Invalid credentials");
        }

        var result = await _signIn.PasswordSignInAsync(user, password, isPersistent: true, lockoutOnFailure: true);
        if (!result.Succeeded) return Unauthorized("Invalid credentials");

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
        return Redirect("/");
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return Redirect("/");
    }

    [HttpGet("denied")]
    [AllowAnonymous]
    public IActionResult Denied() => Content("Access Denied");
}
