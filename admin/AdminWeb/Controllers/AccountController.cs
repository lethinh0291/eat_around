using System.Security.Claims;
using AdminWeb.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminWeb.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly IConfiguration _configuration;
    private readonly AdminManagementApiClient _adminManagementApiClient;

    public AccountController(IConfiguration configuration, AdminManagementApiClient adminManagementApiClient)
    {
        _configuration = configuration;
        _adminManagementApiClient = adminManagementApiClient;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new LoginViewModel
        {
            ReturnUrl = returnUrl
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var expectedUsername = _configuration["AdminAuth:Username"] ?? string.Empty;
        var expectedPassword = _configuration["AdminAuth:Password"] ?? string.Empty;

        var loginResult = await _adminManagementApiClient.AuthenticateAsync(model.Username, model.Password);

        // Keep config-based admin login as fallback for emergency access.
        if (loginResult is null &&
            string.Equals(model.Username, expectedUsername, StringComparison.Ordinal) &&
            string.Equals(model.Password, expectedPassword, StringComparison.Ordinal))
        {
            loginResult = new AdminManagementApiClient.LoginResultDto
            {
                Name = expectedUsername,
                Username = expectedUsername,
                Email = string.Empty,
                Role = "admin"
            };
        }

        if (loginResult is null)
        {
            ModelState.AddModelError(string.Empty, "Sai tên đăng nhập hoặc mật khẩu.");
            return View(model);
        }

        var normalizedRole = (loginResult.Role ?? string.Empty).Trim().ToLowerInvariant();
        if (!string.Equals(normalizedRole, "admin", StringComparison.Ordinal) &&
            !string.Equals(normalizedRole, "seller", StringComparison.Ordinal))
        {
            ModelState.AddModelError(string.Empty, "Tài khoản này không có quyền truy cập admin web.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, string.IsNullOrWhiteSpace(loginResult.Name) ? loginResult.Username : loginResult.Name),
            new(ClaimTypes.NameIdentifier, loginResult.Id.ToString()),
            new(ClaimTypes.Role, normalizedRole),
            new(ClaimTypes.GivenName, loginResult.Username),
            new(ClaimTypes.Email, loginResult.Email)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            });

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        if (string.Equals(normalizedRole, "seller", StringComparison.Ordinal))
        {
            return RedirectToAction("Index", "OwnerPortal");
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    public sealed class LoginViewModel
    {
        public string Username { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public string? ReturnUrl { get; set; }
    }
}