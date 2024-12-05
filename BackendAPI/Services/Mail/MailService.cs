using MailKit.Net.Smtp;
using MimeKit;
using System;
using System.IO;
using System.Threading.Tasks;

namespace BackendAPI.Services
{
    public class MailService
    {
        private readonly string _smtpServer;
        private readonly int _smtpPort;
        private readonly string _smtpUser;
        private readonly string _smtpPass;

        // Parameterized constructor
        public MailService(string smtpServer, int smtpPort, string smtpUser = null, string smtpPass = null)
        {
            _smtpServer = smtpServer;
            _smtpPort = smtpPort;
            _smtpUser = smtpUser;
            _smtpPass = smtpPass;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlBody, string plainTextBody = null)
        {
            var email = new MimeMessage();

            // Configure email sender and recipient
            email.From.Add(new MailboxAddress("NoReply LicentaDemo", _smtpUser));
            email.To.Add(MailboxAddress.Parse(toEmail));
            email.Subject = subject;

            // Build the email body with both HTML and plain text (optional)
            var builder = new BodyBuilder
            {
                HtmlBody = htmlBody,
                TextBody = plainTextBody ?? "This is a fallback plain text version of the email."
            };
            email.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();

            try
            {
                // Connect to the SMTP server with SSL
                await smtp.ConnectAsync(_smtpServer, _smtpPort, MailKit.Security.SecureSocketOptions.SslOnConnect);

                // Authenticate with the SMTP server
                if (!string.IsNullOrEmpty(_smtpUser) && !string.IsNullOrEmpty(_smtpPass))
                {
                    await smtp.AuthenticateAsync(_smtpUser, _smtpPass);
                }

                // Send the email
                await smtp.SendAsync(email);
            }
            catch (Exception ex)
            {
                // Log the error (replace with your logging framework)
                Console.WriteLine($"Error sending email: {ex.Message}");
                throw; // Re-throw the exception to handle it in the calling method
            }
            finally
            {
                // Disconnect from the SMTP server
                await smtp.DisconnectAsync(true);
            }
        }

        public async Task SendVerificationEmail(string email, string salt)
        {
            const string subject = "Verify Your Email";

            try
            {
                // Define the path to the HTML template
                var basePath = AppDomain.CurrentDomain.BaseDirectory;
                var bodyPath = Path.Combine(basePath, "assets", "mailBodies", "verifyEmailBody.html");

                if (!File.Exists(bodyPath))
                {
                    throw new FileNotFoundException("Email body template file not found.", bodyPath);
                }

                // Read and prepare the HTML email body
                var htmlBody = await File.ReadAllTextAsync(bodyPath);
                
                if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
                {
                    htmlBody = htmlBody.Replace("{{url}}", "http://locahost:3000/verify-user?s=" + salt);
                }
                else
                {
                    htmlBody = htmlBody.Replace("{{url}}", "https://licenta.stefandanieluta.ro/verify-user/?=" + salt);
                }

                // Send the email with an optional plain text body
                var plainTextBody = $"Please use the following verification code: {salt}";
                await SendEmailAsync(email, subject, htmlBody, plainTextBody);
            }
            catch (FileNotFoundException ex)
            {
                // Log file-related errors
                Console.WriteLine($"Error reading email template: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                // Log generic errors
                Console.WriteLine($"Error sending verification email: {ex.Message}");
                throw;
            }
        }
    }
}
