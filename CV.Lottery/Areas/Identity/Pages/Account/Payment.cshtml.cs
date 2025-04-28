using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Stripe;
using Stripe.Checkout;
using CV.Lottery.Models;
using Microsoft.EntityFrameworkCore;
using CV.Lottery.Context;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace CV.Lottery.Areas.Identity.Pages.Account
{
    public class PaymentModel : PageModel
    {
        private readonly IConfiguration _config;
        private readonly LotteryContext _lotteryContext;
        private readonly UserManager<IdentityUser> _userManager;
        public bool PaymentSuccess { get; set; }
        public bool PaymentFailed { get; set; }
        public int AttemptCount { get; set; }
        [BindProperty(SupportsGet = true)]
        public string UserId { get; set; }
        public decimal PaymentAmount { get; set; }

        public PaymentModel(IConfiguration config, LotteryContext lotteryContext, UserManager<IdentityUser> userManager)
        {
            _config = config;
            _lotteryContext = lotteryContext;
            _userManager = userManager;
        }

        public void OnGet(string userId)
        {
            UserId = userId;
            AttemptCount = HttpContext.Session.GetInt32("PaymentAttempt") ?? 0;

            // Fetch latest ACTIVE event from LuckyDrawMaster and set PaymentAmount
            var latestActiveEvent = _lotteryContext.LuckyDrawMaster
                .Where(e => e.IsActive == true && e.EventDate >= DateTime.UtcNow.Date)
                .FirstOrDefault();
            if (latestActiveEvent != null)
            {
                PaymentAmount = (decimal)latestActiveEvent.Amount ;
                ViewData["EventId"] = latestActiveEvent.Id;
            }
            else
            {
                PaymentAmount = 0;
                ViewData["EventId"] = null;
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            AttemptCount = HttpContext.Session.GetInt32("PaymentAttempt") ?? 0;
            if (AttemptCount >= 3)
            {
                PaymentFailed = true;
                return Page();
            }

            string body;
            using (var reader = new StreamReader(Request.Body))
            {
                body = await reader.ReadToEndAsync();
            }
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(body);
            string paymentMethodId = data != null && data.ContainsKey("paymentMethodId") ? data["paymentMethodId"] : null;

            var paymentResult = await ProcessStripePaymentAsync(paymentMethodId);

            if (paymentResult.success)
            {
                PaymentSuccess = true;
                HttpContext.Session.Remove("PaymentAttempt");

                // --- NEW: Retrieve registration data from session ---
                var registrationJson = HttpContext.Session.GetString("PendingRegistration");
                if (!string.IsNullOrEmpty(registrationJson))
                {
                    var reg = System.Text.Json.JsonSerializer.Deserialize<RegistrationSessionModel>(registrationJson);
                    // Only create LotteryUser, do NOT create AspNetUser
                    var lotteryUser = new LotteryUsers
                    {
                        Email = reg.Email,
                        FirstName = reg.FirstName,
                        MiddleName = reg.MiddleName,
                        UserName = reg.FirstName + " " + reg.LastName,
                        LastName = reg.LastName,
                        Country = reg.Country,
                        StreetLine1 = reg.StreetLine1,
                        StreetLine2 = reg.StreetLine2,
                        City = reg.City,
                        State = reg.State,
                        ZipCode = reg.ZipPostal,
                        Mobile = reg.Mobile,
                        Home = reg.Home,
                        CreatedOn = DateTime.UtcNow,
                        IsActive = true,
                        UserId = reg.Email, // Use Email as unique identifier for non-Identity users
                        CreatedBy = null // Allow null as per your DB schema
                    };
                    _lotteryContext.LotteryUsers.Add(lotteryUser);
                    await _lotteryContext.SaveChangesAsync();
                    // Save Payment as before
                    var latestActiveEvent = _lotteryContext.LuckyDrawMaster.Where(e => e.IsActive == true && e.EventDate >= DateTime.UtcNow.Date).FirstOrDefault();
                    int? eventId = latestActiveEvent?.Id;
                    var payment = new Payments
                    {
                        UsersId = lotteryUser.Id,
                        Transaction = paymentResult.transactionId,
                        PaymentStatus = "Paid",
                        CreatedBy = lotteryUser.Id.ToString(),
                        CreatedOn = DateTime.UtcNow,
                        IsActive = true,
                        EventId = eventId,
                        Amount = latestActiveEvent.Amount
                    };
                    _lotteryContext.Payments.Add(payment);
                    await _lotteryContext.SaveChangesAsync();
                    // Remove registration from session
                    HttpContext.Session.Remove("PendingRegistration");
                }
                return new JsonResult(new { success = true, redirect = Url.Page("/Index") });
            }
            else
            {
                AttemptCount++;
                HttpContext.Session.SetInt32("PaymentAttempt", AttemptCount);
                PaymentFailed = true;
                if (AttemptCount >= 3)
                {
                    // Optionally clear registration
                    HttpContext.Session.Remove("PendingRegistration");
                    return new JsonResult(new { redirect = Url.Page("/Identity/NewRegisterPage") });
                }
                return new JsonResult(new { error = paymentResult.errorMessage });
            }
        }

        private string GetFallbackUserId()
        {
            // Try to find a user who has not paid yet
            var unpaidUser = _lotteryContext.LotteryUsers
                .FirstOrDefault(u => !_lotteryContext.Payments.Any(p => p.UsersId == u.Id && p.PaymentStatus == "Paid"));
            return unpaidUser?.UserId;
        }

        private async Task<(bool success, string transactionId, string errorMessage)> ProcessStripePaymentAsync(string paymentMethodId)
        {
            if (string.IsNullOrEmpty(paymentMethodId))
                return (false, null, "No payment method provided.");

            try
            {
                StripeConfiguration.ApiKey = _config["Stripe:SecretKey"];
                // Step 1: Create PaymentIntent with automatic payment methods and allow_redirects = never
                var createOptions = new PaymentIntentCreateOptions
                {
                    Amount = 50000, // Amount in cents
                    Currency = "inr",
                    AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
                    {
                        Enabled = true,
                        AllowRedirects = "never"
                    }
                };
                var service = new PaymentIntentService();
                var intent = await service.CreateAsync(createOptions);

                // Step 2: Confirm PaymentIntent with payment method
                var confirmOptions = new PaymentIntentConfirmOptions
                {
                    PaymentMethod = paymentMethodId
                };
                var confirmedIntent = await service.ConfirmAsync(intent.Id, confirmOptions);

                if (confirmedIntent.Status == "succeeded")
                {
                    return (true, confirmedIntent.Id, null);
                }
                else
                {
                    return (false, confirmedIntent.Id, $"Payment failed: {confirmedIntent.Status}");
                }
            }
            catch (StripeException ex)
            {
                return (false, null, ex.Message);
            }
        }
    }

    // Helper model for session registration data
    public class RegistrationSessionModel
    {
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string MiddleName { get; set; }
        public string LastName { get; set; }
        public string Country { get; set; }
        public string StreetLine1 { get; set; }
        public string StreetLine2 { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string ZipPostal { get; set; }
        public string Mobile { get; set; }
        public string Home { get; set; }
        public string Password { get; set; }
    }
}
