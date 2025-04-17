using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using CV.Lottery.Models;
using CV.Lottery.Context;

namespace CV.Lottery.Areas.Identity.Pages
{
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
        public int DrawNumber { get; set; } = 17; // Example, update as needed
        public bool WinnerDeclared { get; set; }
        public string WinnerName { get; set; }

        public class PaidUserDto
        {
            public string UserId { get; set; }
            public string UserName { get; set; }
            public string Email { get; set; }
        }

        public async Task OnGetAsync()
        {
            // Fetch paid payments and join with LotteryUsers to get user details
            PaidUsers = (from p in _lotteryContext.Payments
                         join u in _lotteryContext.LotteryUsers on p.UsersId equals u.Id
                         where p.PaymentStatus == "Paid"
                         select new PaidUserDto
                         {
                             UserId = u.Id.ToString(),
                             UserName = u.UserName,
                             Email = u.Email
                         }).ToList();
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
            if (!string.IsNullOrEmpty(WinnerUserId))
            {
                var winnerUser = _lotteryContext.LotteryUsers.FirstOrDefault(u => u.Id.ToString() == WinnerUserId);
                if (winnerUser != null)
                {
                    // Save to Winner table
                    var winnerEntity = new Winner
                    {
                        UsersId = winnerUser.Id,
                        CreatedBy = winnerUser.Id.ToString(),
                        CreatedOn = DateTime.UtcNow,
                        IsActive = true
                    };
                    _lotteryContext.Winner.Add(winnerEntity);
                    await _lotteryContext.SaveChangesAsync();
                    WinnerDeclared = true;
                    WinnerName = winnerEntity.UsersId.ToString();
                }
            }
            return RedirectToPage();
        }
    }
}
