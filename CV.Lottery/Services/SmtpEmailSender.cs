using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using System.IO;

namespace CV.Lottery.Services
{
    public class SmtpEmailSender : IEmailSender
    {
        private readonly IConfiguration _configuration;
        public SmtpEmailSender(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            var smtpSection = _configuration.GetSection("Smtp");
            var host = smtpSection["Host"];
            var port = int.Parse(smtpSection["Port"]);
            var enableSsl = bool.Parse(smtpSection["EnableSsl"]);
            var user = smtpSection["User"];
            var password = smtpSection["Password"];
            var senderEmail = smtpSection["SenderEmail"];
            var senderName = smtpSection["SenderName"];

            var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(user, password),
                EnableSsl = enableSsl,
                UseDefaultCredentials = false,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 20000 // 20 seconds
            };

            var mailMessage = new MailMessage()
            {
                From = new MailAddress(senderEmail, senderName),
                Subject = subject,
                Body = htmlMessage,
                IsBodyHtml = true
            };
            mailMessage.To.Add(email);
            try
            {
                await client.SendMailAsync(mailMessage);
            }
            catch (SmtpException smtpEx)
            {
                // Log more details for SMTP errors
                System.IO.File.AppendAllText("smtp_error.log", $"{DateTime.Now}: SMTP ERROR: {smtpEx.StatusCode} - {smtpEx.Message}\n{smtpEx.StackTrace}\n");
                throw;
            }
            catch (Exception ex)
            {
                // Log the exception to a file for debugging
                System.IO.File.AppendAllText("smtp_error.log", $"{DateTime.Now}: {ex.Message}\n{ex.StackTrace}\n");
                throw; // rethrow to allow upstream handling
            }
        }
    }
}
