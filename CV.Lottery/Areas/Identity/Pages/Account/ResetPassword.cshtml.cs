using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace CV.Lottery.Areas.Identity.Pages.Account
{
    public class ResetPasswordModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;

        public ResetPasswordModel(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;

        }

        [BindProperty]
        public InputModel Input { get; set; }
        public string StatusMessage { get; set; }
        public string UserName { get; set; }

        public class InputModel
        {
            [Required]
            public string Code { get; set; }

            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "New Password")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }
        }

        public IActionResult OnGet(string code = null, string email = null)
        {
            if (code == null || email == null)
            {
                StatusMessage = "A code and email must be supplied for password reset.";
                return Page();
            }
            Input = new InputModel
            {
                Code = code,
                Email = email
            };
            // Lookup username from email
            var user = _userManager.FindByEmailAsync(email).GetAwaiter().GetResult();
            UserName = user != null ? user.UserName : string.Empty;
            return Page();
        }

        //public async Task<IActionResult> OnPostAsync()
        //{
        //    if (!ModelState.IsValid)
        //    {
        //        return Page();
        //    }
        //    var user = await _userManager.FindByEmailAsync(Input.Email);

        //    // Clear session
        //    HttpContext.Session.Clear();

        //    // Sign out user (clears authentication cookies)
        //    await _signInManager.SignOutAsync();

        //    foreach (var cookie in Request.Cookies.Keys)
        //    {
        //        Response.Cookies.Delete(cookie);
        //    }

        //    if (user == null)
        //    {
        //        // Don't reveal that the user does not exist
        //        StatusMessage = "Password reset successful.";
        //        return RedirectToPage("/Account/Login");
        //    }
        //    var decodedCode = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(Input.Code));
        //    var result = await _userManager.ResetPasswordAsync(user, decodedCode, Input.Password);
        //    if (result.Succeeded)
        //    {
        //        StatusMessage = "Password reset successful.";
        //        return RedirectToPage("/Account/Login");
        //    }
        //    foreach (var error in result.Errors)
        //    {
        //        ModelState.AddModelError(string.Empty, error.Description);
        //    }
        //    return Page();
        //}

        public async Task<IActionResult> OnPostAsync(string source = null)
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await _userManager.FindByEmailAsync(Input.Email);

            // Clear session and log out only if coming from the login page
            if (source == "/Account/Login")
            {
                HttpContext.Session.Clear();
                await _signInManager.SignOutAsync();

                foreach (var cookie in Request.Cookies.Keys)
                {
                    Response.Cookies.Delete(cookie);
                }
            }

            if (user == null)
            {
                StatusMessage = "Password reset successful.";
                TempData["ToastMessage"] = "Password reset successful.";
                return source == "admin" ? RedirectToPage("/createadmin") : RedirectToPage("/Account/Login");
            }

            var decodedCode = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(Input.Code));
            var result = await _userManager.ResetPasswordAsync(user, decodedCode, Input.Password);

            if (result.Succeeded)
            {
                StatusMessage = "Password reset successful.";
                TempData["ToastMessage"] = "Your password has been reset successfully!";
                return source != "admin" ? RedirectToPage("/createadmin") : RedirectToPage("/Account/Login");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }


    }
}
