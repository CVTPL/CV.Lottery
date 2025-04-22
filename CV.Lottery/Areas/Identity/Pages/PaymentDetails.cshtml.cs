using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using CV.Lottery.Context;

namespace CV.Lottery.Areas.Identity.Pages
{
    [Authorize(Roles = "admin")]
    public class PaymentDetailsModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly LotteryContext _lotteryContext;

        public PaymentDetailsModel(UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager, IHttpContextAccessor httpContextAccessor, LotteryContext lotteryContext)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _httpContextAccessor = httpContextAccessor;
            _lotteryContext = lotteryContext;
        }

        public int PageNumber { get; set; }
        public int TotalPages { get; set; }
        public List<DashboardModel.EventSummary> AllEvents { get; set; }

        public async Task<IActionResult> OnGetAsync(int pageNumber = 1)
        {
            if (User == null || User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return RedirectToPage("/Account/Login");
            }

            // Fetch the latest active LuckyDrawMaster event for event name/date
            var luckyDraw = _lotteryContext.LuckyDrawMaster
                .Where(e => e.IsActive == true)
                .OrderByDescending(e => e.EventDate)
                .FirstOrDefault();
            string eventName = luckyDraw?.EventName ?? "No Active Event";
            DateTime? winnerAnnouncementDate = luckyDraw?.EventDate;

            // Fetch all LotteryUsers and filter in-memory by ASP.NET Identity roles
            var allLotteryUsers = _lotteryContext.LotteryUsers.ToList();
            var usersWithUserRole = new List<CV.Lottery.Models.LotteryUsers>();
            foreach (var lotteryUser in allLotteryUsers)
            {
                var roles = await _userManager.GetRolesAsync(new IdentityUser { Id = lotteryUser.UserId });
                if (roles.Contains("user"))
                {
                    usersWithUserRole.Add(lotteryUser);
                }
            }

            var users = usersWithUserRole
                .Select(u => {
                    var latestPayment = _lotteryContext.Payments
                        .Where(p => p.UsersId == u.Id)
                        .OrderByDescending(p => p.CreatedOn)
                        .FirstOrDefault();
                    return new {
                        User = u,
                        LatestPayment = latestPayment,
                        PaidOn = latestPayment?.CreatedOn ?? u.CreatedOn
                    };
                })
                .ToList();

            var allEvents = users
                .Select(x => new DashboardModel.EventSummary
                {
                    UserName = x.User.UserName,
                    EventName = eventName,
                    WinnerAnnouncementDate = winnerAnnouncementDate ?? DateTime.Now,
                    PaymentStatus = x.LatestPayment != null && !string.IsNullOrEmpty(x.LatestPayment.PaymentStatus) ? x.LatestPayment.PaymentStatus : "Not Paid",
                    Amount = (x.LatestPayment != null) ? x.LatestPayment.Amount : 0,
                    UserId = x.User.Id,
                    PaidOn = x.PaidOn ?? DateTime.MinValue
                })
                .OrderByDescending(e => e.UserId)
                .ToList();

            PageNumber = pageNumber;
            int PageSize = 10;
            TotalPages = (int)Math.Ceiling(allEvents.Count / (double)PageSize);
            AllEvents = allEvents.Skip((PageNumber - 1) * PageSize).Take(PageSize).ToList();
            return Page();
        }
    }
}
