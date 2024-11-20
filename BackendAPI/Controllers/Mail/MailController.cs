using Microsoft.AspNetCore.Mvc;
using BackendAPI.Services;
using System.Threading.Tasks;

namespace BackendAPI.Controllers
{
    [ApiController]
    [Route("/[controller]")]
    public class MailController : ControllerBase
    {
        private readonly MailService _mailService;

        public MailController(IConfiguration configuration)
        {
            var smtpHost = configuration["Smtp:Host"];
            var smtpPort = int.Parse(configuration["Smtp:Port"]);
            var smtpUser = configuration["Smtp:User"];
            var smtpPassword = configuration["Smtp:Password"];

            
            if (!string.IsNullOrEmpty(smtpUser) && !string.IsNullOrEmpty(smtpPassword))
            {
                _mailService = new MailService(smtpHost, smtpPort, smtpUser, smtpPassword);
            }
            else
            {
                _mailService = new MailService(smtpHost, smtpPort);
            }
        }

        [HttpPost("sendVerificationEmail")]
        public async Task<IActionResult> SendVerificationEmail([FromBody] string email)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@p_email", email }
            };

            var response = await MySqlDatabaseService.Instance.ExecuteQueryAsync("SELECT salt FROM users WHERE email = @p_email", parameters);
            if (response.Count == 0)
                return BadRequest("User not found");

            var salt = (string)response[0]["salt"];

            await _mailService.sendVerificationEmail(email, salt);
            return Ok();
        }
    }

}
