using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using CV.Lottery.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;
using System.Security.Claims;

namespace CV.Lottery.Areas.Identity.Pages
{
    public class NewRegisterModel : PageModel
    {
        [BindProperty]
        public InputModel Input { get; set; }

        public List<SelectListItem> CountryList { get; set; }

        private readonly LotteryContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public NewRegisterModel(LotteryContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public class InputModel
        {
            [Required(ErrorMessage = "Email is required.")]
            [EmailAddress(ErrorMessage = "Invalid email address.")]
            public string Email { get; set; }

            [Required(ErrorMessage = "First name is required.")]
            public string FirstName { get; set; }

            // Middle name is optional
            public string MiddleName { get; set; }

            [Required(ErrorMessage = "Last name is required.")]
            public string LastName { get; set; }

            [Required(ErrorMessage = "Country is required.")]
            public string Country { get; set; }

            [Required(ErrorMessage = "Street Line 1 is required.")]
            public string StreetLine1 { get; set; }

            // Street Line 2 is optional
            public string StreetLine2 { get; set; }

            [Required(ErrorMessage = "City is required.")]
            public string City { get; set; }

            [Required(ErrorMessage = "State is required.")]
            public string State { get; set; }

            [Required(ErrorMessage = "Zip/Postal Code is required.")]
            public string ZipPostal { get; set; }

            [Required(ErrorMessage = "Mobile is required.")]
            public string Mobile { get; set; }

            // Home is optional
            public string Home { get; set; }
        }

        public void OnGet()
        {
            CountryList = GetCountryList();
            if (Input == null)
                Input = new InputModel();
            if (string.IsNullOrEmpty(Input.Country))
                Input.Country = "United States";
        }

        private List<SelectListItem> GetCountryList()
        {
            // Fetch countries from the database, ordered by Name
            var countries = _context.Country.OrderBy(c => c.Name).ToList();
            var list = countries.Select(c => new SelectListItem { Value = c.Value, Text = c.Name }).ToList();
            return list;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            CountryList = GetCountryList(); // Ensure CountryList is always populated for the view
            // Remove ModelState errors for optional fields (prevents aria-invalid)
            ModelState.Remove("Input.MiddleName");
            ModelState.Remove("Input.StreetLine2");
            ModelState.Remove("Input.Home");
            if (!ModelState.IsValid)
            {
                // If AJAX, return the partial form only
                if (Request.Headers["X-Requested-With"].ToString().Equals("XMLHttpRequest", System.StringComparison.OrdinalIgnoreCase))
                {
                    // Use the full path to the partial to avoid view location issues
                    return Partial("~/Areas/Identity/Pages/_NewRegisterFormPartial.cshtml", this);
                }
                return Page();
            }

            // Check if email already exists in AspNetUsers
            var existingIdentityUser = await _userManager.FindByEmailAsync(Input.Email);
            if (existingIdentityUser != null)
            {
                ModelState.AddModelError("Input.Email", "An account with this email already exists.");
                return Page();
            }
            // Check if email already exists in LotteryUsers (for extra safety)
            var existingLotteryUser = _context.LotteryUsers.FirstOrDefault(u => u.Email.ToLower() == Input.Email.ToLower());
            if (existingLotteryUser != null)
            {
                ModelState.AddModelError("Input.Email", "An account with this email already exists in the lottery system.");
                return Page();
            }

            // Ensure optional fields are never null
            if (string.IsNullOrWhiteSpace(Input.MiddleName)) Input.MiddleName = string.Empty;
            if (string.IsNullOrWhiteSpace(Input.StreetLine2)) Input.StreetLine2 = string.Empty;
            if (string.IsNullOrWhiteSpace(Input.Home)) Input.Home = string.Empty;

            // Store registration data in session and redirect to payment
            var registrationData = new
            {
                Input.Email,
                Input.FirstName,
                Input.MiddleName,
                Input.LastName,
                Input.Country,
                Input.StreetLine1,
                Input.StreetLine2,
                Input.City,
                Input.State,
                Input.ZipPostal,
                Input.Mobile,
                Input.Home
            };
            var registrationJson = System.Text.Json.JsonSerializer.Serialize(registrationData);
            HttpContext.Session.SetString("PendingRegistration", registrationJson);
            // Redirect to payment page
            return RedirectToPage("/Account/Payment");
        }
    }
}
