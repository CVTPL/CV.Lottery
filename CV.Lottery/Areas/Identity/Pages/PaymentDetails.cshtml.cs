using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using CV.Lottery.Context;
using CV.Lottery.Models;
using Microsoft.IdentityModel.Tokens;

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
        public int TotalUsers { get; set; }
        public List<DashboardModel.EventSummary> AllEvents { get; set; }

        [BindProperty(SupportsGet = true)]
        public string SearchUserName { get; set; }

        [BindProperty(SupportsGet = true)]
        public int SelectedEvent { get; set; }

        [BindProperty(SupportsGet = true)]
        public string SortColumn { get; set; }
        [BindProperty(SupportsGet = true)]
        public string SortDirection { get; set; } // "asc" or "desc"

        public List<LuckyDrawMaster> EventList { get; set; }

        public async Task<IActionResult> OnGetAsync(int pageNumber = 1)
        {
            //EventList = _lotteryContext.LuckyDrawMaster
            //                .OrderByDescending(e => e.EventDate)
            //                .ToList();

            //// Removed manual authentication check to allow [Authorize] and Identity middleware to handle redirects

            //// Fetch the latest active LuckyDrawMaster event for event name/date
            //var luckyDraw = SelectedEvent == 0
            //        ? _lotteryContext.LuckyDrawMaster
            //            .FirstOrDefault(e => e.EventDate >= DateTime.UtcNow.Date)
            //        : _lotteryContext.LuckyDrawMaster
            //            .FirstOrDefault(e => e.Id == SelectedEvent);

            //string eventName = luckyDraw?.EventName ?? "No Active Event";
            //DateTime? winnerAnnouncementDate = luckyDraw?.EventDate;

            //// Fetch only users who have payments for the active event
            //var activeEventId = luckyDraw?.Id;
            //var usersWithUserRole = _lotteryContext.LotteryUsers
            //    .Where(u => _lotteryContext.Payments.Any(p => p.UsersId == u.Id && p.EventId == activeEventId))
            //    .ToList();

            EventList = _lotteryContext.LuckyDrawMaster
                    .OrderByDescending(e => e.EventDate)
                    .ToList();

            // Get selected event or latest one
            LuckyDrawMaster? luckyDraw = null;

            if (SelectedEvent != 0)
            {
                luckyDraw = _lotteryContext.LuckyDrawMaster.FirstOrDefault(e => e.Id == SelectedEvent);
            }
            else
            {
                luckyDraw = _lotteryContext.LuckyDrawMaster
                    .OrderByDescending(e => e.EventDate)
                    .FirstOrDefault();
            }

            int? selectedEventId = SelectedEvent != 0 ? SelectedEvent : (int?)null;

            // Get users with payments for the selected event or all if no selection
            var usersWithUserRole = _lotteryContext.LotteryUsers
                .Where(u => _lotteryContext.Payments.Any(p =>
                    p.UsersId == u.Id && (selectedEventId == null || p.EventId == selectedEventId)))
                .ToList();

            // Search filter for admin
            if (!string.IsNullOrWhiteSpace(SearchUserName))
            {
                usersWithUserRole = usersWithUserRole
                    .Where(u => u.UserName != null && u.UserName.ToLower().Contains(SearchUserName.ToLower()))
                    .ToList();
            }

            // Sort logic
            if (!string.IsNullOrEmpty(SortColumn))
            {
                bool ascending = string.IsNullOrEmpty(SortDirection) || SortDirection.ToLower() == "asc";
                switch (SortColumn.ToLower())
                {
                    case "username":
                        usersWithUserRole = ascending
                            ? usersWithUserRole.OrderBy(u => u.UserName).ToList()
                            : usersWithUserRole.OrderByDescending(u => u.UserName).ToList();
                        break;
                    case "email":
                        usersWithUserRole = ascending
                            ? usersWithUserRole.OrderBy(u => u.Email).ToList()
                            : usersWithUserRole.OrderByDescending(u => u.Email).ToList();
                        break;
                    // Add more columns as needed
                    default:
                        break;
                }
            }

            // After sorting, project to EventSummary and apply paging/sorting to AllEvents only
            var users = usersWithUserRole
                .Select(u => {
                    var latestPayment = _lotteryContext.Payments
                        .Where(p => p.UsersId == u.Id && (selectedEventId == null || p.EventId == selectedEventId))
                        .OrderByDescending(p => p.CreatedOn)
                        .FirstOrDefault();
                    return new DashboardModel.EventSummary
                    {
                        UserName = u.UserName,
                        EventName = luckyDraw?.EventName ?? "",
                        WinnerAnnouncementDate = luckyDraw?.EventDate ?? DateTime.MinValue,
                        PaidOn = latestPayment?.CreatedOn ?? DateTime.MinValue,
                        PaymentStatus = latestPayment?.PaymentStatus ?? "Not Paid",
                        Amount = latestPayment?.Amount ?? 0,
                        UserId = u.Id,
                        Email = u.Email, // Add Email property
                        PhoneNumber = u.Mobile // Use Mobile property for phone number
                    };
                })
                .ToList();

            // Restore TotalUsers calculation
            TotalUsers = users.Count;

            // Filter to only paid users for this event
            AllEvents = users.Where(e => e.PaymentStatus == "Paid").ToList();

            // Apply sorting to AllEvents (the grid)
            IEnumerable<DashboardModel.EventSummary> sortedEvents = AllEvents;
            if (!string.IsNullOrEmpty(SortColumn))
            {
                bool ascending = string.IsNullOrEmpty(SortDirection) || SortDirection.ToLower() == "asc";
                switch (SortColumn.ToLower())
                {
                    case "username":
                        sortedEvents = ascending
                            ? AllEvents.OrderBy(e => e.UserName)
                            : AllEvents.OrderByDescending(e => e.UserName);
                        break;
                    case "email":
                        sortedEvents = ascending
                            ? AllEvents.OrderBy(e => e.Email)
                            : AllEvents.OrderByDescending(e => e.Email);
                        break;
                    case "phonenumber":
                        sortedEvents = ascending
                            ? AllEvents.OrderBy(e => e.PhoneNumber)
                            : AllEvents.OrderByDescending(e => e.PhoneNumber);
                        break;
                    case "amount":
                        sortedEvents = ascending
                            ? AllEvents.OrderBy(e => e.Amount)
                            : AllEvents.OrderByDescending(e => e.Amount);
                        break;
                    //case "paymentstatus":
                    //    sortedEvents = ascending
                    //        ? AllEvents.OrderBy(e => e.PaymentStatus)
                    //        : AllEvents.OrderByDescending(e => e.PaymentStatus);
                    //    break;
                    case "paidon":
                        sortedEvents = ascending
                            ? AllEvents.OrderBy(e => e.PaidOn)
                            : AllEvents.OrderByDescending(e => e.PaidOn);
                        break;
                    default:
                        break;
                }
            }
            else
            {
                sortedEvents = AllEvents.OrderByDescending(e => e.UserId);
            }

            int PageSize = 10;
            PageNumber = pageNumber;
            TotalPages = (int)Math.Ceiling(sortedEvents.Count() / (double)PageSize);
            AllEvents = sortedEvents.Skip((PageNumber - 1) * PageSize).Take(PageSize).ToList();
            return Page();
        }
    }
}
