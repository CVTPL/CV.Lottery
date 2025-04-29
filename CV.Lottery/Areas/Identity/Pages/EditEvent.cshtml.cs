using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CV.Lottery.Models;
using System.ComponentModel.DataAnnotations;
using CV.Lottery.Context;

namespace CV.Lottery.Areas.Identity.Pages
{
    public class EditEventModel : PageModel
    {
        private readonly LotteryContext _context;
        public EditEventModel(LotteryContext context)
        {
            _context = context;
        }
        [BindProperty]
        public InputModel Input { get; set; }
        public class InputModel
        {
            [Required]
            [Display(Name = "Event Name")]
            public string EventName { get; set; }
            [Required]
            [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
            public decimal Amount { get; set; }
        }
        public IActionResult OnGet(int? eventId)
        {
            if (!eventId.HasValue)
                return RedirectToPage("Dashboard");
            var evt = _context.LuckyDrawMaster.FirstOrDefault(e => e.Id == eventId.Value);
            if (evt == null)
                return RedirectToPage("Dashboard");
            Input = new InputModel
            {
                EventName = evt.EventName,
                Amount = (decimal)evt.Amount
            };
            return Page();
        }
        public IActionResult OnPost(int? eventId)
        {
            if (!eventId.HasValue)
                return RedirectToPage("Dashboard");
            if (!ModelState.IsValid)
                return Page();
            var evt = _context.LuckyDrawMaster.FirstOrDefault(e => e.Id == eventId.Value);
            if (evt == null)
                return RedirectToPage("Dashboard");
            evt.EventName = Input.EventName;
            evt.Amount = Input.Amount;
            _context.SaveChanges();
            return RedirectToPage("Dashboard");
        }
    }
}
