using Microsoft.AspNetCore.Mvc.RazorPages;
using CV.Lottery.Context;
using System.Linq;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CV.Lottery.Areas.Identity.Pages
{
    public class IndexModel : PageModel
    {
        private readonly LotteryContext _lotteryContext;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        public string EventName { get; set; }
        public decimal Amount { get; set; }
        public int EventId { get; set; }

        public IndexModel(LotteryContext lotteryContext, UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager)
        {
            _lotteryContext = lotteryContext;
            _userManager = userManager;
            _signInManager = signInManager;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var user = await _userManager.GetUserAsync(User);
                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Contains("admin"))
                {
                    HttpContext.Session.Clear();

                    await _signInManager.SignOutAsync();

                    foreach (var cookie in Request.Cookies.Keys)
                    {
                        Response.Cookies.Delete(cookie);
                    }
                    // Redirect admin users to the login page using a full redirect
                    return Redirect("/login");
                }
            }
            // Get top 1 active event (no OrderByDescending)
            var luckyDraw = _lotteryContext.LuckyDrawMaster
                .FirstOrDefault(e => e.IsActive == true && e.EventDate >= DateTime.UtcNow.Date);
            EventName = luckyDraw?.EventName ?? "No Active Event";
            Amount = luckyDraw?.Amount ?? 0;
            EventId = luckyDraw?.Id ?? 0;
            return Page();
        }
    }
}
