using CV.Lottery.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace CV.Lottery.Areas.Identity.Pages
{
    [Authorize(Roles = "admin")]
    public class CreateAdminModel : PageModel
    {
        public readonly UserManager<IdentityUser> _userManager;
        public readonly RoleManager<IdentityRole> _roleManager;
        private readonly LotteryContext _lotteryContext;

        public CreateAdminModel(UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager, LotteryContext lotteryContext)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _lotteryContext = lotteryContext;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            [Display(Name = "Username")]
            [StringLength(32, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 3)]
            public string Username { get; set; }

            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [Required]
            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }
        }

        public List<CV.Lottery.Models.LotteryUsers> AllUsers { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; } = 10;
        public int TotalPages { get; set; }

        public async Task<IActionResult> OnGetAsync(int pageNumber = 1)
        {
            // Get all users (including admins and users)
            var usersQuery = _lotteryContext.LotteryUsers.OrderByDescending(u => u.Id);
            int totalUsers = await usersQuery.CountAsync();
            PageNumber = pageNumber;
            TotalPages = (int)Math.Ceiling(totalUsers / (double)PageSize);
            AllUsers = await usersQuery.Skip((PageNumber - 1) * PageSize).Take(PageSize).ToListAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            // Create Identity user: set UserName/email logic as requested
            var user = new IdentityUser {
                UserName = Input.Email, // Store email in UserName (as per user role logic)
                NormalizedUserName = Input.Email.ToUpperInvariant(),
                Email = Input.Email,
                EmailConfirmed = true
            };
            var result = await _userManager.CreateAsync(user, Input.Password);
            if (result.Succeeded)
            {
                // Ensure admin role exists
                if (!await _roleManager.RoleExistsAsync("admin"))
                {
                    await _roleManager.CreateAsync(new IdentityRole("admin"));
                }
                await _userManager.AddToRoleAsync(user, "admin");

                // Save to LotteryUsers table as well
                var aspNetUserId = user.Id; // This is the Id from AspNetUsers table
                var lotteryUser = new CV.Lottery.Models.LotteryUsers
                {
                    UserId = aspNetUserId, // Store AspNetUsers.Id
                    Email = Input.Email,
                    UserName = Input.Username, // Store username in LotteryUsers.UserName
                    CreatedBy = aspNetUserId, // Store AspNetUsers.Id
                    CreatedOn = DateTime.UtcNow,
                    IsActive = true
                };
                _lotteryContext.LotteryUsers.Add(lotteryUser);
                await _lotteryContext.SaveChangesAsync();

                TempData["AdminCreated"] = "Admin user created successfully.";
                return RedirectToPage("/CreateAdmin");
            }
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return Page();
        }
    }
}
