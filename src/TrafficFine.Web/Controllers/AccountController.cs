using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TrafficFine.Web.Domain;
using TrafficFine.Web.ViewModels;
namespace TrafficFine.Web.Controllers;
public sealed class AccountController(SignInManager<AppUser> signIn) : Controller
{
    [AllowAnonymous, HttpGet]
    public IActionResult Login(string? returnUrl = null) => View(new LoginForm { ReturnUrl = returnUrl });
    [AllowAnonymous, HttpPost]
    public async Task<IActionResult> Login(LoginForm form)
    {
        if (!ModelState.IsValid) return View(form);
        var result = await signIn.PasswordSignInAsync(form.Email.Trim(), form.Password, false, lockoutOnFailure: true);
        if (result.Succeeded) return LocalRedirect(Url.IsLocalUrl(form.ReturnUrl) ? form.ReturnUrl! : "/");
        ModelState.AddModelError("", result.IsLockedOut ? "Çok fazla deneme. Beş dakika sonra tekrar deneyin." : "E-posta veya şifre hatalı.");
        return View(form);
    }
    [Authorize, HttpPost]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }
    [AllowAnonymous, HttpGet]
    public IActionResult AccessDenied() { Response.StatusCode = 403; return View(); }
}
