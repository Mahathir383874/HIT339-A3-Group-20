using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Bibliotheca.Models.Entities;
using Bibliotheca.Models.ViewModels;

namespace Bibliotheca.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToDashboard();
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(
                model.Email,
                model.Password,
                model.RememberMe,
                lockoutOnFailure: false);

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Successfully logged in!";
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                return RedirectToDashboard();
            }

            ModelState.AddModelError(string.Empty, "Invalid login credentials. Please check your email and password.");
            return View(model);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuickLogin(string role)
        {
            string email = role switch
            {
                "Admin" => "admin@bibliotheca.com",
                "Reception" => "reception@bibliotheca.com",
                "Manager" => "manager@bibliotheca.com",
                _ => "admin@bibliotheca.com"
            };

            string password = role switch
            {
                "Admin" => "Admin@123",
                "Reception" => "Reception@123",
                "Manager" => "Manager@123",
                _ => "Admin@123"
            };

            var result = await _signInManager.PasswordSignInAsync(email, password, isPersistent: false, lockoutOnFailure: false);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = $"Logged in as {role} ({email})";
                return RedirectToDashboard();
            }

            TempData["ErrorMessage"] = $"Could not quick-login as {role}. Please ensure database is seeded.";
            return RedirectToAction(nameof(Login));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            TempData["InfoMessage"] = "You have been logged out.";
            return RedirectToAction("Index", "Catalog");
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }

        private IActionResult RedirectToDashboard()
        {
            if (User.IsInRole("Admin"))
            {
                return RedirectToAction("Index", "Items");
            }
            if (User.IsInRole("Reception"))
            {
                return RedirectToAction("Index", "Loans");
            }
            if (User.IsInRole("Manager"))
            {
                return RedirectToAction("Index", "Manager");
            }

            return RedirectToAction("Index", "Catalog");
        }
    }
}
