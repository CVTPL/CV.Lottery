using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CV.Lottery.Models;
using System.Linq;
using CV.Lottery.Context;
using System.ComponentModel;

namespace CV.Lottery.Areas.Identity.Pages
{
    public class EventCreateModel : PageModel, IValidatableObject
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
        [Range(1, double.MaxValue, ErrorMessage = "Amount must be at least $1")]
        public decimal Amount { get; set; }

        [BindProperty]
        [Required]
        [DataType(DataType.Date)]
        public DateTime WinnerAnnouncementDate { get; set; }

        [BindProperty]
        public int? EventId { get; set; }

        public void OnGet()
        {
            // Set default value for datepicker to current date
            if (WinnerAnnouncementDate == default(DateTime))
            {
                WinnerAnnouncementDate = DateTime.UtcNow.Date;
            }
        }

        // Handler for partial view (AJAX modal)
        public async Task<IActionResult> OnGetPartialAsync(int? eventId = null)
        {
            if (eventId.HasValue)
            {
                var evt = await _context.LuckyDrawMaster.FindAsync(eventId.Value);
                if (evt != null)
                {
                    EventId = evt.Id;
                    EventName = evt.EventName;
                    Amount = (decimal)evt.Amount;
                    WinnerAnnouncementDate = (DateTime)evt.EventDate;
                }
            }
            else if (WinnerAnnouncementDate == default(DateTime))
            {
                WinnerAnnouncementDate = DateTime.UtcNow.Date;
            }
            return Partial("_EventCreatePartial", this);
        }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (WinnerAnnouncementDate < DateTime.UtcNow.Date)
            {
                yield return new ValidationResult("Winner Announcement Date must be today or a future date.", new[] { nameof(WinnerAnnouncementDate) });
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

        // Handler for AJAX modal POST
        public async Task<IActionResult> OnPostPartialAsync(int? eventId = null)
        {
            if (!ModelState.IsValid)
            {
                // Return the partial view with validation errors
                return Partial("_EventCreatePartial", this);
            }

            LuckyDrawMaster luckyDraw;
            if (eventId.HasValue)
            {
                luckyDraw = await _context.LuckyDrawMaster.FindAsync(eventId.Value);
                if (luckyDraw == null)
                {
                    return new JsonResult(new { success = false, message = "Event not found." });
                }
                luckyDraw.EventName = EventName;
                luckyDraw.Amount = Amount;
                luckyDraw.EventDate = WinnerAnnouncementDate;
                luckyDraw.UpdatedOn = DateTime.UtcNow;
            }
            else
            {
                var aspUser = await _userManager.GetUserAsync(User);
                var lotteryUser = _context.LotteryUsers.FirstOrDefault(u => u.UserId == aspUser.Id);
                var adminUserId = lotteryUser?.Id;
                luckyDraw = new LuckyDrawMaster
                {
                    EventName = EventName,
                    Amount = Amount,
                    EventDate = WinnerAnnouncementDate,
                    IsActive = true,
                    CreatedOn = DateTime.UtcNow,
                    CreatedBy = adminUserId?.ToString()
                };
                _context.LuckyDrawMaster.Add(luckyDraw);
            }
            await _context.SaveChangesAsync();
            return new JsonResult(new { success = true });
        }
    }
}
