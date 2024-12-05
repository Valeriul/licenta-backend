using Microsoft.AspNetCore.Mvc;
using BackendAPI.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BackendAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MailController : ControllerBase
    {
        private readonly MailService _mailService;
        private readonly IConfiguration _configuration;

        public MailController(IConfiguration configuration)
        {
            _configuration = configuration;

            
            var smtpHost = _configuration["SMTP:Host"];
            var smtpPort = int.Parse(_configuration["SMTP:Port"]);
            var smtpUser = _configuration["SMTP:Username"];
            var smtpPassword = _configuration["SMTP:Password"];

            _mailService = new MailService(smtpHost, smtpPort, smtpUser, smtpPassword);

        }

        [HttpPost("sendVerificationEmail")]
        public async Task<IActionResult> SendVerificationEmail([FromBody] string email)
        {
            try
            {
                if (string.IsNullOrEmpty(email))
                {
                    return BadRequest("Email address cannot be empty.");
                }

                
                var parameters = new Dictionary<string, object>
                {
                    { "@p_email", email }
                };

                var response = await MySqlDatabaseService.Instance.ExecuteQueryAsync("SELECT salt FROM users WHERE email = @p_email", parameters);

                if (response.Count == 0)
                {
                    return NotFound("User not found.");
                }

                var salt = response[0]["salt"]?.ToString();

                if (string.IsNullOrEmpty(salt))
                {
                    return StatusCode(500, "Salt for user not found.");
                }

                
                await _mailService.SendVerificationEmail(email, salt);
                return Ok("Verification email sent successfully.");
            }
            catch (Exception ex)
            {
                
                Console.WriteLine($"Error occurred: {ex.Message}");

                
                return StatusCode(500, "An error occurred while sending the email.");
            }
        }
    }
}
