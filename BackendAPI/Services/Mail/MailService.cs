using MailKit.Net.Smtp;
using MimeKit;
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

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            var email = new MimeMessage();
            email.From.Add(new MailboxAddress("Your App", _smtpUser ?? "no-reply@localhost"));
            email.To.Add(MailboxAddress.Parse(toEmail));
            email.Subject = subject;

            var builder = new BodyBuilder { HtmlBody = body };
            email.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();
            try
            {
                await smtp.ConnectAsync(_smtpServer, _smtpPort, MailKit.Security.SecureSocketOptions.StartTlsWhenAvailable);

                
                if (!string.IsNullOrEmpty(_smtpUser) && !string.IsNullOrEmpty(_smtpPass))
                {
                    await smtp.AuthenticateAsync(_smtpUser, _smtpPass);
                }

                await smtp.SendAsync(email);
            }
            finally
            {
                await smtp.DisconnectAsync(true);
            }
        }

        public async Task sendVerificationEmail(string email, string salt)
        {
            var subject = "Verify your email";
            
            var body = System.IO.File.ReadAllText("./assets/mailBodies/verifyEmailBody.html");
            body = body.Replace("{{salt}}", salt);

            await SendEmailAsync(email, subject, body);
        }
    }
}
