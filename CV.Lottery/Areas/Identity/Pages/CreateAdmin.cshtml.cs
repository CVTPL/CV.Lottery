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

            // Check if username or email already exists (uniqueness check)
            // Username uniqueness: check both LotteryUsers and display_username claim in Identity
            var existingLotteryUser = await _lotteryContext.LotteryUsers.FirstOrDefaultAsync(u => u.UserName == Input.Username);
            if (existingLotteryUser != null)
            {
                ModelState.AddModelError("Input.Username", "Username is already taken.");
                return Page();
            }
            // Check username in display_username claim for all users
            var allUsers = _userManager.Users.ToList();
            foreach (var u in allUsers)
            {
                var claims = await _userManager.GetClaimsAsync(u);
                if (claims.Any(c => c.Type == "display_username" && c.Value == Input.Username))
                {
                    ModelState.AddModelError("Input.Username", "Username is already taken.");
                    return Page();
                }
            }
            // Email uniqueness: check both Identity and LotteryUsers
            var existingIdentityUser = await _userManager.FindByEmailAsync(Input.Email);
            if (existingIdentityUser != null)
            {
                ModelState.AddModelError("Input.Email", "Email is already registered.");
                return Page();
            }
            var existingLotteryEmail = await _lotteryContext.LotteryUsers.FirstOrDefaultAsync(u => u.Email == Input.Email);
            if (existingLotteryEmail != null)
            {
                ModelState.AddModelError("Input.Email", "Email is already registered.");
                return Page();
            }

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

                // Add claim for admin role (for AspNetUserClaims table)
                var adminClaim = new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "admin");
                await _userManager.AddClaimAsync(user, adminClaim);

                // Add claim for display_username (value from Username textbox)
                var displayUsernameClaim = new System.Security.Claims.Claim("display_username", Input.Username);
                await _userManager.AddClaimAsync(user, displayUsernameClaim);

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
