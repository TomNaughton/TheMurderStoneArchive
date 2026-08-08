using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TheMurderStoneArchive.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;

        public RegisterModel(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Required]
            [StringLength(100, MinimumLength = 6)]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Compare("Password")]
            public string ConfirmPassword { get; set; }
        }

        public void OnGet(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            if (!ModelState.IsValid)
            {
                // Collect ModelState errors so they are visible in the validation summary
                var allErrors = new System.Text.StringBuilder();
                foreach (var kv in ModelState)
                {
                    foreach (var err in kv.Value.Errors)
                    {
                        if (!string.IsNullOrEmpty(err.ErrorMessage)) allErrors.AppendLine(err.ErrorMessage);
                        else if (err.Exception != null) allErrors.AppendLine(err.Exception.Message);
                    }
                }
                if (allErrors.Length > 0)
                {
                    TempData["DebugErrors"] = allErrors.ToString();
                }

                return Page();
            }

            // If a user with this email already exists, surface a friendly message
            var existingUser = await _userManager.FindByEmailAsync(Input.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError(string.Empty, "An account with this email already exists. Please sign in or reset your password.");
                return Page();
            }

            var user = new IdentityUser { UserName = Input.Email, Email = Input.Email };
            IdentityResult result;
            try
            {
                result = await _userManager.CreateAsync(user, Input.Password);
            }
            catch (System.Exception ex)
            {
                ModelState.AddModelError(string.Empty, "Registration error: " + ex.Message);
                TempData["DebugErrors"] = ex.ToString();
                return Page();
            }
            if (result.Succeeded)
            {
                // Sign the user in immediately after registration (not persistent)
                await _signInManager.SignInAsync(user, isPersistent: false);

                // Always redirect new users to the Home/Index page
                return RedirectToAction("Index", "Home");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return Page();
        }
    }
}
