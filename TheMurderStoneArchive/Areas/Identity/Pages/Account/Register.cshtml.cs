using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TheMurderStoneArchive.Models;

namespace TheMurderStoneArchive.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;
        private readonly System.Net.Http.IHttpClientFactory _httpClientFactory;

        public RegisterModel(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, Microsoft.Extensions.Configuration.IConfiguration configuration, System.Net.Http.IHttpClientFactory httpClientFactory)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
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
            [StringLength(30, MinimumLength = 3)]
            [RegularExpression("^[a-zA-Z0-9_-]+$", ErrorMessage = "Username may only contain letters, numbers, hyphens and underscores.")]
            [Display(Name = "Public username")]
            public string PublicUsername { get; set; }

            [Required]
            [StringLength(100, MinimumLength = 8)]
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
            // Validate captcha first (reCAPTCHA v3)
            var token = Request.Form["g-recaptcha-response"].ToString();
            if (string.IsNullOrEmpty(token) || !await VerifyReCaptchaAsync(token, "register"))
            {
                ModelState.AddModelError(string.Empty, "Captcha verification failed. Please try again.");
                return Page();
            }
            if (!ModelState.IsValid)
            {
                return Page();
            }

            // If a user with this email already exists, surface a friendly message
            var existingUser = await _userManager.FindByEmailAsync(Input.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError(string.Empty, "An account with this email already exists. Please sign in or reset your password.");
                return Page();
            }

            var existingUsername = await _userManager.Users.FirstOrDefaultAsync(u => u.PublicUsername == Input.PublicUsername);
            if (existingUsername != null)
            {
                ModelState.AddModelError("Input.PublicUsername", "This username is already taken. Please choose another.");
                return Page();
            }

            var user = new ApplicationUser { UserName = Input.Email, Email = Input.Email, PublicUsername = Input.PublicUsername };
            IdentityResult result;
            try
            {
                result = await _userManager.CreateAsync(user, Input.Password);
            }
            catch (InvalidOperationException)
            {
                ModelState.AddModelError(string.Empty, "Registration is temporarily unavailable. Please try again later.");
                return Page();
            }
            catch (System.Exception)
            {
                ModelState.AddModelError(string.Empty, "An unexpected error occurred during registration. Please try again later.");
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

        private async Task<bool> VerifyReCaptchaAsync(string token, string expectedAction = null, double minScore = 0.5)
        {
            try
            {
                var secret = _configuration["ReCaptcha:SecretKey"];
                if (string.IsNullOrEmpty(secret)) return false;
                var client = _httpClientFactory.CreateClient();
                var values = new System.Collections.Generic.Dictionary<string, string>
                {
                    {"secret", secret},
                    {"response", token}
                };
                var content = new System.Net.Http.FormUrlEncodedContent(values);
                var resp = await client.PostAsync("https://www.google.com/recaptcha/api/siteverify", content);
                if (!resp.IsSuccessStatusCode) return false;
                var json = await resp.Content.ReadAsStringAsync();
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("success", out var success) || !success.GetBoolean())
                    return false;

                // Only check score for reCAPTCHA v3 (v2 doesn't return a score)
                if (doc.RootElement.TryGetProperty("score", out var scoreElem) && scoreElem.ValueKind == System.Text.Json.JsonValueKind.Number)
                {
                    double score = scoreElem.GetDouble();
                    if (score < minScore)
                        return false;
                }

                if (!string.IsNullOrEmpty(expectedAction))
                {
                    if (doc.RootElement.TryGetProperty("action", out var actionElem))
                    {
                        var action = actionElem.GetString();
                        if (!string.Equals(action, expectedAction, StringComparison.OrdinalIgnoreCase))
                            return false;
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
