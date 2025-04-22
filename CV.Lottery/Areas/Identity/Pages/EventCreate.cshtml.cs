using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CV.Lottery.Models;
using System.Linq;
using CV.Lottery.Context;

namespace CV.Lottery.Areas.Identity.Pages
{
    public class EventCreateModel : PageModel
    {
        private readonly LotteryContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public EventCreateModel(LotteryContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [BindProperty]
        [Required]
        public string EventName { get; set; }

        [BindProperty]
        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "Amount must be positive")]
        public decimal Amount { get; set; }

        [BindProperty]
        [Required]
        [DataType(DataType.Date)]
        public DateTime WinnerAnnouncementDate { get; set; }

        public void OnGet()
        {
            // Set default value for datepicker to current date
            if (WinnerAnnouncementDate == default(DateTime))
            {
                WinnerAnnouncementDate = DateTime.UtcNow.Date;
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Get current admin user's Id (from LotteryUsers)
            var aspUser = await _userManager.GetUserAsync(User);
            var lotteryUser = _context.LotteryUsers.FirstOrDefault(u => u.UserId == aspUser.Id);
            var adminUserId = lotteryUser?.Id;

            var luckyDraw = new LuckyDrawMaster
            {
                EventName = EventName,
                Amount = Amount,
                EventDate = WinnerAnnouncementDate,
                IsActive = true,
                CreatedOn = DateTime.UtcNow,
                CreatedBy = adminUserId?.ToString()
            };
            _context.LuckyDrawMaster.Add(luckyDraw);
            await _context.SaveChangesAsync();
            return RedirectToPage("/Dashboard");
        }
    }
}
