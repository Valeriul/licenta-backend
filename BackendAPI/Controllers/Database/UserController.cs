using System.Threading.Tasks;
using BackendAPI.Services;
using Microsoft.AspNetCore.Mvc;
using BackendAPI.Models;

namespace BackendAPI.Controllers
{
    [ApiController]
    [Route("/[controller]")]

    public class UserController : ControllerBase
    {
        private readonly UserService _userService;

        public UserController()
        {
            _userService = new UserService();
        }

        [HttpPost("register")]
        public async Task<IActionResult> RegisterUser([FromBody] UserRegister user)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                await _userService.RegisterUser(user);
                return Ok();
            }
            catch (MySqlConnector.MySqlException ex)
            {
                switch (ex.Number)
                {
                    case 1062:
                        return BadRequest(new { message = "Email already in use" });
                    default:
                        return StatusCode(500, new { message = "An unexpected error occured" });
                }
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An unexpected error occured" });
            }
        }

        [HttpGet("isVerified")]
        public async Task<IActionResult> IsUserVerified([FromQuery] string salt)
        {
            var result = await _userService.IsUserVerified(salt);
            return Ok(result);
        }

        [HttpPost("verifyUser")]
        public async Task<IActionResult> verifyResult([FromBody] UserVerification user)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            
            string webSocketUrl = "ws://" + user.CentralURL + ":5002/ws";
            string uuid_Central = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(webSocketUrl));

            
            var parameters = new System.Collections.Generic.Dictionary<string, object>
            {
                { "@p_salt", user.Salt },
                { "@p_centralURL", uuid_Central }
            };

            
            try
            {
                var response = await MySqlDatabaseService.Instance.ExecuteQueryAsync("UPDATE users SET isVerified = 1, uuid_Central = @p_centralURL WHERE salt = @p_salt", parameters);
                response = await MySqlDatabaseService.Instance.ExecuteQueryAsync("SELECT id_user FROM users WHERE salt = @p_salt", parameters);

                ulong id = (ulong)response[0]["id_user"];

                await BackendAPI.Services.WebSocketManager.Instance.AddWebSocketAsync(id, webSocketUrl);
                return Ok();
            }
            catch (Exception e)
            {
                return StatusCode(500, new { message = "An unexpected error occured" });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserLogin user)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            
            var parameters = new System.Collections.Generic.Dictionary<string, object>
            {
                { "@p_email", user.Email },
                { "@p_unhashed_password", user.Password }
            };

            
            var response = await MySqlDatabaseService.Instance.ExecuteStoredProcedureAsync("GetLoggedUser", parameters);

            
            if (response != null && response != DBNull.Value)
            {
                
                return Ok(new { user_id = Convert.ToInt64(response) });
            }

            
            return Unauthorized("Invalid email or password");
        }


        [HttpPost("loginWithSalt")]

        public async Task<IActionResult> LoginWithSalt([FromBody] UserLoginWithSalt user)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var parameters = new System.Collections.Generic.Dictionary<string, object>
            {
                { "@p_salt", user.Salt }
            };

            var response = await MySqlDatabaseService.Instance.ExecuteQueryAsync("SELECT id_user FROM users WHERE salt = @p_salt", parameters);
            return Ok(response[0]["id_user"]);
        }
    }
}
