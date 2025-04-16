using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CV.Lottery.Areas.Identity.Pages
{
    [Authorize]
    public class DashboardModel : PageModel
    {
        public string EventName { get; set; }
        public DateTime WinnerAnnouncementDate { get; set; }
        public string PaymentStatus { get; set; }

        public void OnGet()
        {
            // TODO: Replace with real data fetching logic
            EventName = "Spring Lottery 2025";
            WinnerAnnouncementDate = new DateTime(2025, 5, 1);
            PaymentStatus = "Paid"; // Can be "Paid", "Pending", "Failed"
        }
    }
}
