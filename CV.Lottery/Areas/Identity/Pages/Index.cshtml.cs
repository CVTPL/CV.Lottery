using Microsoft.AspNetCore.Mvc.RazorPages;
using CV.Lottery.Context;
using System.Linq;

namespace CV.Lottery.Areas.Identity.Pages
{
    public class IndexModel : PageModel
    {
        private readonly LotteryContext _lotteryContext;
        public string EventName { get; set; }
        public decimal Amount { get; set; }

        public IndexModel(LotteryContext lotteryContext)
        {
            _lotteryContext = lotteryContext;
        }

        public void OnGet()
        {
            // Get top 1 active event (no OrderByDescending)
            var luckyDraw = _lotteryContext.LuckyDrawMaster
                .Where(e => e.IsActive == true && e.EventDate >= DateTime.UtcNow.Date)
                .FirstOrDefault();
            EventName = luckyDraw?.EventName ?? "No Active Event";
            Amount = luckyDraw?.Amount ?? 0;
        }
    }
}
