using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using CV.Lottery.Models;
using CV.Lottery.Context;

namespace CV.Lottery.Areas.Identity.Pages
{
    [Authorize(Roles = "admin")]
    public class WinnerSelectionModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;

        private readonly LotteryContext _lotteryContext;

        public WinnerSelectionModel(UserManager<IdentityUser> userManager, LotteryContext lotteryContext)
        {
            _userManager = userManager;
            _lotteryContext = lotteryContext;
        }

        public List<PaidUserDto> PaidUsers { get; set; } = new();
        [BindProperty]
        public string SelectedUserId { get; set; }
        [BindProperty]
        public string WinnerUserId { get; set; }
        [BindProperty]
        public int EventId { get; set; }
        public int DrawNumber { get; set; } = 17; // Example, update as needed
        public bool WinnerDeclared { get; set; }
        public string WinnerName { get; set; }
        public bool WinnerSaved { get; set; }

        public class PaidUserDto
        {
            public string UserId { get; set; }
            public string UserName { get; set; }
            public string Email { get; set; }
        }

        public async Task OnGetAsync(int eventId = 0, bool winnerSaved = false)
        {
            EventId = eventId;
            // Fetch paid payments for the selected event and join with LotteryUsers to get user details
            PaidUsers = (from p in _lotteryContext.Payments
                         join u in _lotteryContext.LotteryUsers on p.UsersId equals u.Id
                         where p.PaymentStatus == "Paid" && p.EventId == eventId
                         select new PaidUserDto
                         {
                             UserId = u.Id.ToString(),
                             UserName = u.UserName,
                             Email = u.Email
                         }).ToList();
            WinnerSaved = winnerSaved;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await OnGetAsync();
            if (!string.IsNullOrEmpty(SelectedUserId))
            {
                var winner = PaidUsers.FirstOrDefault(u => u.UserId == SelectedUserId);
                if (winner != null)
                {
                    WinnerDeclared = true;
                    WinnerName = winner.UserName;
                    // TODO: Save winner to DB or event
                }
            }
            return Page();
        }

        public async Task<IActionResult> OnPostSaveWinnerAsync()
        {
            // DO NOT call OnGetAsync here, it will overwrite EventId from POST
            if (!string.IsNullOrEmpty(WinnerUserId) && EventId > 0)
            {
                // Find the winner user
                var lotteryUser = _lotteryContext.LotteryUsers.FirstOrDefault(u => u.Id.ToString() == WinnerUserId);
                // Get the current logged-in admin user
                var aspUser = await _userManager.GetUserAsync(User);
                var adminLotteryUser = _lotteryContext.LotteryUsers.FirstOrDefault(u => u.UserId == aspUser.Id);
                var adminUserId = adminLotteryUser?.Id.ToString();
                if (lotteryUser != null && !string.IsNullOrEmpty(adminUserId))
                {
                    var winner = new Winner
                    {
                        UsersId = lotteryUser.Id,
                        EventId = EventId.ToString(),
                        CreatedOn = DateTime.Now,
                        IsActive = true,
                        CreatedBy = adminUserId, // Save logged-in admin's LotteryUsers.Id
                    };
                    _lotteryContext.Winner.Add(winner);
                    _lotteryContext.SaveChanges();
                    WinnerSaved = true;
                }
            }
            // Always redirect with eventId so draw panel is repopulated
            return RedirectToPage(new { winnerSaved = WinnerSaved, eventId = EventId });
        }
    }
}
