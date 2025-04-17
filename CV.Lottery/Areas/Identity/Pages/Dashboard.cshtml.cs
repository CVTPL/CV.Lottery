using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Linq;

namespace CV.Lottery.Areas.Identity.Pages
{
    [Authorize]
    public class DashboardModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly RoleManager<IdentityRole> _roleManager;

        public DashboardModel(UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager, IHttpContextAccessor httpContextAccessor)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _httpContextAccessor = httpContextAccessor;
        }

        public string EventName { get; set; }
        public DateTime WinnerAnnouncementDate { get; set; }
        public string PaymentStatus { get; set; }
        public string WinnerName { get; set; }

        // Admin view
        public List<EventSummary> AllEvents { get; set; } = new List<EventSummary>();
        public bool IsAdmin { get; set; }

        public class EventSummary
        {
            public string UserName { get; set; }
            public string EventName { get; set; }
            public DateTime WinnerAnnouncementDate { get; set; }
            public string PaymentStatus { get; set; }
            public decimal Amount { get; set; }
        }

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(user);
            IsAdmin = roles.Contains("admin");

            if (IsAdmin)
            {
                // TODO: Replace with real data fetching for all events and payments
                AllEvents = new List<EventSummary>
                {
                    new EventSummary { UserName = "Joy Patel", EventName = "Spring Lottery 2025", WinnerAnnouncementDate = new DateTime(2025, 5, 1), PaymentStatus = "Paid", Amount = 500 },
                    new EventSummary { UserName = "Alex Smith", EventName = "Winter Raffle 2024", WinnerAnnouncementDate = new DateTime(2024, 12, 15), PaymentStatus = "Pending", Amount = 250 },
                    new EventSummary { UserName = "Sara Lee", EventName = "Summer Bonanza", WinnerAnnouncementDate = new DateTime(2025, 7, 10), PaymentStatus = "Failed", Amount = 300 }
                };
            }
            else
            {
                // TODO: Replace with real data fetching for current user
                EventName = "Spring Lottery 2025";
                WinnerAnnouncementDate = new DateTime(2025, 5, 1);
                PaymentStatus = "Paid";
                // Set winner name based on whether event is over
                if (WinnerAnnouncementDate <= DateTime.Now)
                {
                    WinnerName = "Joy Patel"; // TODO: Replace with real winner
                }
                else
                {
                    WinnerName = string.Empty;
                }
            }
        }
    }
}
