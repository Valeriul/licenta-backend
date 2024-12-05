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

            
            email.From.Add(new MailboxAddress("NoReply LicentaDemo", _smtpUser));
            email.To.Add(MailboxAddress.Parse(toEmail));
            email.Subject = subject;

            
            var builder = new BodyBuilder
            {
                HtmlBody = htmlBody,
                TextBody = plainTextBody ?? "This is a fallback plain text version of the email."
            };
            email.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();

            try
            {
                
                await smtp.ConnectAsync(_smtpServer, _smtpPort, MailKit.Security.SecureSocketOptions.SslOnConnect);

                
                if (!string.IsNullOrEmpty(_smtpUser) && !string.IsNullOrEmpty(_smtpPass))
                {
                    await smtp.AuthenticateAsync(_smtpUser, _smtpPass);
                }

                
                await smtp.SendAsync(email);
            }
            catch (Exception ex)
            {
                
                Console.WriteLine($"Error sending email: {ex.Message}");
                throw; 
            }
            finally
            {
                
                await smtp.DisconnectAsync(true);
            }
        }

        public async Task SendVerificationEmail(string email, string salt)
        {
            const string subject = "Verify Your Email";

            try
            {
                
                var basePath = AppDomain.CurrentDomain.BaseDirectory;
                var bodyPath = Path.Combine(basePath, "assets", "mailBodies", "verifyEmailBody.html");

                if (!File.Exists(bodyPath))
                {
                    throw new FileNotFoundException("Email body template file not found.", bodyPath);
                }

                
                var htmlBody = await File.ReadAllTextAsync(bodyPath);
                
                if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
                {
                    htmlBody = htmlBody.Replace("{{url}}", "http://locahost:3000/verify-user?s=" + salt);
                }
                else
                {
                    htmlBody = htmlBody.Replace("{{url}}", "https://licenta.stefandanieluta.ro/verify-user/?=" + salt);
                }

                
                var plainTextBody = $"Please use the following verification code: {salt}";
                await SendEmailAsync(email, subject, htmlBody, plainTextBody);
            }
            catch (FileNotFoundException ex)
            {
                
                Console.WriteLine($"Error reading email template: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                
                Console.WriteLine($"Error sending verification email: {ex.Message}");
                throw;
            }
        }
    }
}
