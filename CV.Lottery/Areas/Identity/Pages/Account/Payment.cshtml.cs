using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Stripe;
using Stripe.Checkout;
using CV.Lottery.Models;
using Microsoft.EntityFrameworkCore;
using CV.Lottery.Context;
using System.Text.Json;

namespace CV.Lottery.Areas.Identity.Pages.Account
{
    public class PaymentModel : PageModel
    {
        private readonly IConfiguration _config;
        private readonly LotteryContext _lotteryContext;
        public bool PaymentSuccess { get; set; }
        public bool PaymentFailed { get; set; }
        public int AttemptCount { get; set; }
        [BindProperty(SupportsGet = true)]
        public string UserId { get; set; }
        public decimal PaymentAmount { get; set; }

        public PaymentModel(IConfiguration config, LotteryContext lotteryContext)
        {
            _config = config;
            _lotteryContext = lotteryContext;
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

                // Save payment entry in DB
                string userId = UserId;
                // Fallback: try to get from request body (if you send it from frontend)
                if (string.IsNullOrEmpty(userId) && data != null && data.TryGetValue("userId", out var userIdFromBody))
                {
                    userId = userIdFromBody;
                }
                // Final fallback: try to get the first unpaid user from the DB
                if (string.IsNullOrEmpty(userId))
                {
                    userId = GetFallbackUserId();
                }
                decimal amount = 0;
                if (data != null && data.TryGetValue("amount", out var amountStr))
                {
                    decimal.TryParse(amountStr, out amount);
                }
                var lotteryUser = _lotteryContext.LotteryUsers.FirstOrDefault(u => u.UserId == userId);
                if (lotteryUser != null)
                {
                    var latestActiveEvent = _lotteryContext.LuckyDrawMaster
                        .Where(e => e.IsActive == true && e.EventDate >= DateTime.UtcNow.Date)
                        .FirstOrDefault();
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
                        Amount = amount
                    };
                    _lotteryContext.Payments.Add(payment);
                    await _lotteryContext.SaveChangesAsync();
                }
                return new JsonResult(new { redirect = Url.Page("/Account/Login") });
            }
            else
            {
                AttemptCount++;
                HttpContext.Session.SetInt32("PaymentAttempt", AttemptCount);
                PaymentFailed = true;
                if (AttemptCount >= 3)
                {
                    string userId = UserId;
                    if (string.IsNullOrEmpty(userId) && data != null && data.TryGetValue("userId", out var userIdFromBody))
                    {
                        userId = userIdFromBody;
                    }
                    if (string.IsNullOrEmpty(userId))
                    {
                        userId = GetFallbackUserId();
                    }
                    decimal amount = 0;
                    if (data != null && data.TryGetValue("amount", out var amountStr))
                    {
                        decimal.TryParse(amountStr, out amount);
                    }
                    var lotteryUser = _lotteryContext.LotteryUsers.FirstOrDefault(u => u.UserId == userId);
                    if (lotteryUser != null)
                    {
                        var latestActiveEvent = _lotteryContext.LuckyDrawMaster
                            .Where(e => e.IsActive == true && e.EventDate >= DateTime.UtcNow.Date)
                            .FirstOrDefault();
                        int? eventId = latestActiveEvent?.Id;

                        var payment = new Payments
                        {
                            UsersId = lotteryUser.Id,
                            Transaction = paymentResult.transactionId ?? Guid.NewGuid().ToString(),
                            PaymentStatus = "Failed",
                            CreatedBy = lotteryUser.Id.ToString(),
                            CreatedOn = DateTime.UtcNow,
                            IsActive = false,
                            EventId = eventId,
                            Amount = amount
                        };
                        _lotteryContext.Payments.Add(payment);
                        await _lotteryContext.SaveChangesAsync();
                    }
                    return new JsonResult(new { redirect = Url.Page("/Account/Login") });
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
}
