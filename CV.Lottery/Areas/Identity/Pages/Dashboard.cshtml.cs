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
        public int TotalPaidUsers { get; set; }
        public int TotalNotPaidUsers { get; set; }
        public int TotalUsers { get; set; }
        public List<LuckyDrawMaster> LuckyDrawEvents { get; set; } = new List<LuckyDrawMaster>();
        public List<LuckyDrawEventWithWinner> LuckyDrawEventsWithWinners { get; set; } = new List<LuckyDrawEventWithWinner>();

        [BindProperty]
        public int SelectedEventId { get; set; }

        public class EventSummary
        {
            public string UserName { get; set; }
            public string EventName { get; set; }
            public DateTime WinnerAnnouncementDate { get; set; }
            public DateTime PaidOn { get; set; }
            public string PaymentStatus { get; set; }
            public decimal Amount { get; set; }
            public int UserId { get; set; }
            public string Email { get; set; } // Added for PaymentDetails grid
        }

        public class PaymentDetail
        {
            public string PaymentStatus { get; set; }
            public decimal Amount { get; set; }
            public string CardLast4 { get; set; }
            public string TransactionId { get; set; }
            public DateTime? PaidOn { get; set; }
        }

        public class LuckyDrawEventWithWinner
        {
            public LuckyDrawMaster Event { get; set; }
            public List<string> WinnerNames { get; set; } = new List<string>();
        }

        public PaymentDetail UserPaymentDetail { get; set; }

        public async Task<IActionResult> OnGetAsync(int pageNumber = 1)
        {
            if (User == null || User.Identity == null || !User.Identity.IsAuthenticated)
            {
                // Redirect to login if user is not authenticated
                return RedirectToPage("/Account/Login");
            }
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToPage("/Account/Login");
            }
            var roles = await _userManager.GetRolesAsync(user);
            IsAdmin = roles.Contains("admin");

            if (!IsAdmin && roles.Contains("user"))
            {
                var lotteryUser = _lotteryContext.LotteryUsers.FirstOrDefault(u => u.UserId == user.Id);
                if (lotteryUser != null)
                {
                    var payment = _lotteryContext.Payments
                        .Where(p => p.UsersId == lotteryUser.Id)
                        .OrderByDescending(p => p.CreatedOn)
                        .FirstOrDefault();
                    if (payment == null || payment.PaymentStatus != "Paid")
                    {
                        // Not paid, redirect to payment page
                        return RedirectToPage("/Account/Payment", new { userId = lotteryUser.UserId });
                    }
                }
                else
                {
                    // No lottery user, redirect to payment
                    return RedirectToPage("/Account/Payment");
                }
            }

            if (IsAdmin)
            {
                // Fetch all LuckyDrawMaster events
                var events = _lotteryContext.LuckyDrawMaster.OrderByDescending(e => e.EventDate).ToList();
                var winnersList = _lotteryContext.Winner.ToList();
                var lotteryUsersList = _lotteryContext.LotteryUsers.ToList();
                LuckyDrawEventsWithWinners = events.Select(e => {
                    var eventWinners = winnersList.Where(w => w.EventId == e.Id.ToString()).ToList();
                    var winnerNames = eventWinners
                        .Select(w => lotteryUsersList.FirstOrDefault(u => u.Id == w.UsersId)?.UserName)
                        .Where(name => !string.IsNullOrEmpty(name))
                        .ToList();
                    return new LuckyDrawEventWithWinner
                    {
                        Event = e,
                        WinnerNames = winnerNames
                    };
                }).ToList();
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
                var allLotteryUsers = _lotteryContext.LotteryUsers.ToList();
                var usersWithUserRole = new List<CV.Lottery.Models.LotteryUsers>();
                foreach (var lotteryUser in allLotteryUsers)
                {
                    var userRoles = await _userManager.GetRolesAsync(new IdentityUser { Id = lotteryUser.UserId });
                    if (userRoles.Contains("user"))
                    {
                        usersWithUserRole.Add(lotteryUser);
                    }
                }

                // Find latest payment for each user
                var users = usersWithUserRole
                    .Select(u => {
                        var payment = _lotteryContext.Payments
                            .Where(p => p.UsersId == u.Id)
                            .OrderByDescending(p => p.CreatedOn)
                            .FirstOrDefault();
                        return new {
                            User = u,
                            Payment = payment,
                            PaidOn = payment?.CreatedOn ?? u.CreatedOn
                        };
                    })
                    .ToList();

                var allEvents = users
                    .Select(x => new EventSummary
                    {
                        UserName = x.User.UserName,
                        EventName = EventName,
                        WinnerAnnouncementDate = WinnerAnnouncementDate ?? DateTime.Now,
                        PaymentStatus = x.Payment != null && !string.IsNullOrEmpty(x.Payment.PaymentStatus) ? x.Payment.PaymentStatus : "Not Paid",
                        Amount = (x.Payment != null) ? (decimal)x.Payment.Amount : 0,
                        UserId = x.User.Id,
                        PaidOn = x.PaidOn ?? DateTime.MinValue,
                        Email = x.User.Email // Added for PaymentDetails grid
                    })
                    .OrderByDescending(e => e.UserId)
                    .ToList();

                // Calculate tile values
                TotalPaidUsers = allEvents.Count(e => e.PaymentStatus == "Paid");
                TotalNotPaidUsers = allEvents.Count(e => e.PaymentStatus == "Not Paid" || e.PaymentStatus == "Failed" || e.PaymentStatus == "Pending");
                TotalUsers = TotalPaidUsers + TotalNotPaidUsers;

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
                        Amount = (payment != null && payment.Amount != null) ? (decimal)payment.Amount : 0,
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
            return Page();
        }

        public async Task<IActionResult> OnPostToggleActiveAsync(int id)
        {
            var eventToToggle = _lotteryContext.LuckyDrawMaster.FirstOrDefault(e => e.Id == id);
            if (eventToToggle != null)
            {
                eventToToggle.IsActive = !(eventToToggle.IsActive ?? false);
                _lotteryContext.SaveChanges();
            }
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostSelectWinnerAsync(int id)
        {
            SelectedEventId = id;
            // Store event id logic here (e.g., in session, DB, or process winner selection)
            // Example: TempData["SelectedEventId"] = id;
            // TODO: Implement winner selection logic as needed
            return RedirectToPage();
        }
    }
}
