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
            if (!ModelState.IsValid)
            {
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

            // 1. Create AspNetUser (Identity)
            var identityUser = new IdentityUser
            {
                UserName = Input.Email, // or you may use Input.FirstName + Input.LastName for UserName
                Email = Input.Email,
                EmailConfirmed = true
            };
            var identityResult = await _userManager.CreateAsync(identityUser, "DefaultPassword@123"); // TODO: Replace with actual password logic
            if (!identityResult.Succeeded)
            {
                foreach (var error in identityResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return Page();
            }

            // Add display_username claim
            var displayUserName = Input.FirstName + " " + Input.LastName;
            var claim = new Claim("display_username", displayUserName);
            await _userManager.AddClaimAsync(identityUser, claim);

            // 2. Insert into LotteryUsers
            var lotteryUser = new CV.Lottery.Models.LotteryUsers
            {
                Email = Input.Email,
                UserId = identityUser.Id,
                CreatedBy = identityUser.Id,
                CreatedOn = DateTime.UtcNow,
                IsActive = true,
                UserName = Input.FirstName + " " + Input.LastName,
                FirstName = Input.FirstName,
                MiddleName = Input.MiddleName,
                LastName = Input.LastName,
                Country = Input.Country,
                StreetLine1 = Input.StreetLine1,
                StreetLine2 = Input.StreetLine2,
                City = Input.City,
                State = Input.State,
                ZipCode = Input.ZipPostal,
                Mobile = Input.Mobile,
                Home = Input.Home
                // Add other fields as needed
            };
            _context.LotteryUsers.Add(lotteryUser);
            await _context.SaveChangesAsync();

            // After saving LotteryUser, redirect to Payment page with userId
            return RedirectToPage("/Account/Payment", new { userId = identityUser.Id });
        }
    }
}
