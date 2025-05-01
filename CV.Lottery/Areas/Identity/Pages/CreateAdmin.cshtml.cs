using CV.Lottery.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Threading.Tasks;

namespace CV.Lottery.Areas.Identity.Pages
{
    [Authorize(Roles = "admin")]
    public class CreateAdminModel : PageModel
    {
        public readonly UserManager<IdentityUser> _userManager;
        public readonly RoleManager<IdentityRole> _roleManager;
        private readonly LotteryContext _lotteryContext;
        private readonly IEmailSender _emailSender;

        public CreateAdminModel(UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager, LotteryContext lotteryContext, IEmailSender emailSender)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _lotteryContext = lotteryContext;
            _emailSender = emailSender;
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
            // Get all admin user IDs from Identity
            var adminUsers = await _userManager.GetUsersInRoleAsync("admin");
            var adminUserIds = adminUsers.Select(u => u.Id).ToList();

            // Filter AspNetUsers to only include admins and get username from AspNetUserClaims
            var usersQuery = from user in _lotteryContext.AspNetUsers
                             join claim in _lotteryContext.AspNetUserClaims
                                 on user.Id equals claim.UserId into userClaims
                             from claim in userClaims.Where(c => c.ClaimType == "display_username").DefaultIfEmpty()
                             where adminUserIds.Contains(user.Id)
                             orderby user.Id descending
                             select new CV.Lottery.Models.LotteryUsers {
                                 UserId = user.Id,
                                 UserName = claim != null ? claim.ClaimValue : user.Email, // fallback to Email if claim missing
                                 Email = user.Email
                             };

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
            var existingLotteryUser = await _lotteryContext.LotteryUsers.FirstOrDefaultAsync(u => u.UserName.ToLower() == Input.Username.ToLower());
            if (existingLotteryUser != null)
            {
                ModelState.AddModelError("Input.Username", "Username is already taken.");
                return Page();
            }
            // Check username in AspNetUsers table (case-insensitive, trimmed)
            var existingAspNetUser = await _lotteryContext.AspNetUsers.FirstOrDefaultAsync(u => u.UserName.ToLower() == Input.Username.ToLower());
            if (existingAspNetUser != null)
            {
                ModelState.AddModelError("Input.Username", "Username is already taken in the system (AspNetUsers table).");
                return Page();
            }
            // Check username in display_username claim for all users
            var allUsers = _userManager.Users.ToList();
            foreach (var u in allUsers)
            {
                var claims = await _userManager.GetClaimsAsync(u);
                if (claims.Any(c => c.Type == "display_username" && c.Value.ToLower() == Input.Username.ToLower()))
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
                UserName = Input.Email, // Store the actual username!
                NormalizedUserName = Input.Username.ToUpperInvariant(),
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
                //var aspNetUserId = user.Id; // This is the Id from AspNetUsers table
                //var lotteryUser = new CV.Lottery.Models.LotteryUsers
                //{
                //    UserId = aspNetUserId, // Store AspNetUsers.Id
                //    Email = Input.Email,
                //    UserName = Input.Username, // Store username in LotteryUsers.UserName
                //    CreatedBy = aspNetUserId, // Store AspNetUsers.Id
                //    CreatedOn = DateTime.UtcNow,
                //    IsActive = true
                //};
                //_lotteryContext.LotteryUsers.Add(lotteryUser);
                //await _lotteryContext.SaveChangesAsync();

                TempData["AdminCreated"] = "Admin user created successfully.";
                return RedirectToPage("/CreateAdmin");
            }
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return Page();
        }

        public async Task<IActionResult> OnPostSendResetLinkAsync(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                TempData["ResetError"] = "Email is required.";
                return RedirectToPage();
            }
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
            {
                TempData["ResetError"] = "User not found or email not confirmed.";
                return RedirectToPage();
            }
            var code = await _userManager.GeneratePasswordResetTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code)); // FIXED ENCODING
            // Instead of sending email, redirect admin to the ResetPassword page with code and email in URL
            return Redirect(Url.Page(
                "/Account/ResetPassword",
                pageHandler: null,
                values: new { area = "Identity", code, email },
                protocol: Request.Scheme));
        }
    }
}
