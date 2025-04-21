using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Linq;
using CV.Lottery.Models;
using CV.Lottery.Context;

namespace CV.Lottery.Areas.Identity.Pages
{
    [Authorize]
    public class DashboardModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly LotteryContext _lotteryContext;

        public DashboardModel(UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager, IHttpContextAccessor httpContextAccessor, LotteryContext lotteryContext)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _httpContextAccessor = httpContextAccessor;
            _lotteryContext = lotteryContext;
        }

        public string EventName { get; set; }
        public DateTime? WinnerAnnouncementDate { get; set; }
        public string PaymentStatus => UserPaymentDetail?.PaymentStatus;
        public decimal Amount => UserPaymentDetail?.Amount ?? 0;
        public string TransactionId => UserPaymentDetail?.TransactionId;
        public DateTime? PaidOn => UserPaymentDetail?.PaidOn;
        public string WinnerName { get; set; }

        // Admin view
        public List<EventSummary> AllEvents { get; set; } = new List<EventSummary>();
        public bool IsAdmin { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalPages { get; set; }

        public class EventSummary
        {
            public string UserName { get; set; }
            public string EventName { get; set; }
            public DateTime WinnerAnnouncementDate { get; set; }
            public DateTime PaidOn { get; set; }
            public string PaymentStatus { get; set; }
            public decimal Amount { get; set; }
            public int UserId { get; set; }
        }

        public class PaymentDetail
        {
            public string PaymentStatus { get; set; }
            public decimal Amount { get; set; }
            public string CardLast4 { get; set; }
            public string TransactionId { get; set; }
            public DateTime? PaidOn { get; set; }
        }

        public PaymentDetail UserPaymentDetail { get; set; }

        public async Task OnGetAsync(int pageNumber = 1)
        {
            var user = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(user);
            IsAdmin = roles.Contains("admin");

            if (IsAdmin)
            {
                // Fetch the latest active LuckyDrawMaster event for event name/date
                var luckyDraw = _lotteryContext.LuckyDrawMaster
                    .Where(e => e.IsActive == true)
                    .OrderByDescending(e => e.EventDate)
                    .FirstOrDefault();
                if (luckyDraw != null)
                {
                    EventName = luckyDraw.EventName;
                    WinnerAnnouncementDate = luckyDraw.EventDate;
                }
                else
                {
                    EventName = "No Active Event";
                    WinnerAnnouncementDate = null;
                }
                // Fetch all users and their latest payment (with payment amount and status from Payments table)
                var users = _lotteryContext.LotteryUsers
                    .Select(u => new {
                        User = u,
                        LatestPayment = u.Payments.OrderByDescending(p => p.CreatedOn).FirstOrDefault(),
                        PaidOn = u.CreatedOn
                    })
                    .ToList();
                var allEvents = users
                    .Select(x => new EventSummary
                    {
                        UserName = x.User.UserName,
                        EventName = EventName,
                        WinnerAnnouncementDate = WinnerAnnouncementDate ?? DateTime.Now,
                        PaymentStatus = x.LatestPayment != null && !string.IsNullOrEmpty(x.LatestPayment.PaymentStatus) ? x.LatestPayment.PaymentStatus : "Not Paid",
                        Amount = (x.LatestPayment != null && x.LatestPayment.Amount != null) ? x.LatestPayment.Amount : 0,
                        UserId = x.User.Id,
                        PaidOn = (DateTime)x.PaidOn
                    })
                    .OrderByDescending(e => e.UserId)
                    .ToList();

                PageNumber = pageNumber;
                PageSize = 10;
                TotalPages = (int)Math.Ceiling(allEvents.Count / (double)PageSize);
                AllEvents = allEvents.Skip((PageNumber - 1) * PageSize).Take(PageSize).ToList();
            }
            else
            {
                // Fetch payment details for the logged in user
                var aspUser = await _userManager.GetUserAsync(User);
                var lotteryUser = _lotteryContext.LotteryUsers.FirstOrDefault(u => u.UserId == aspUser.Id);
                // Fetch the latest active LuckyDrawMaster event
                var luckyDraw = _lotteryContext.LuckyDrawMaster
                    .Where(e => e.IsActive == true)
                    .OrderByDescending(e => e.EventDate)
                    .FirstOrDefault();
                if (lotteryUser != null)
                {
                    var payment = _lotteryContext.Payments
                        .Where(p => p.UsersId == lotteryUser.Id)
                        .OrderByDescending(p => p.CreatedOn)
                        .AsEnumerable()
                        .FirstOrDefault();

                    UserPaymentDetail = new PaymentDetail
                    {
                        PaymentStatus = (payment != null && !string.IsNullOrWhiteSpace(payment.PaymentStatus)) ? payment.PaymentStatus : "Not Paid",
                        Amount = (payment != null && payment.Amount != null) ? payment.Amount : 0,
                        CardLast4 = (payment != null && !string.IsNullOrEmpty(payment.Transaction) && payment.Transaction.Length > 4)
                            ? payment.Transaction.Substring(payment.Transaction.Length - 4)
                            : "N/A",
                        TransactionId = (payment != null && !string.IsNullOrEmpty(payment.Transaction)) ? payment.Transaction : "-",
                        PaidOn = payment != null && payment.CreatedOn.HasValue ? payment.CreatedOn : null
                    };
                }
                else
                {
                    UserPaymentDetail = new PaymentDetail
                    {
                        PaymentStatus = "Not Paid",
                        Amount = 0,
                        CardLast4 = "N/A",
                        TransactionId = "-",
                        PaidOn = null
                    };
                }

                // Set event details dynamically from LuckyDrawMaster
                if (luckyDraw != null)
                {
                    EventName = luckyDraw.EventName;
                    WinnerAnnouncementDate = luckyDraw.EventDate;
                }
                else
                {
                    EventName = "No Active Event";
                    WinnerAnnouncementDate = null;
                }
                // Set winner name based on whether event is over
                if (WinnerAnnouncementDate.HasValue && WinnerAnnouncementDate <= DateTime.Now)
                {
                    WinnerName = "--"; // You can replace with logic to fetch real winner
                }
                else
                {
                    WinnerName = string.Empty;
                }
            }
        }
    }
}
