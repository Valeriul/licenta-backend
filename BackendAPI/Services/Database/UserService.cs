using System.Collections.Generic;
using System.Threading.Tasks;
using BackendAPI.Models;

namespace BackendAPI.Services
{
    public class UserService
    {
        public UserService() { }
        public async Task RegisterUser(UserRegister user)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@p_password", user.Password },
                { "@p_email", user.Email },
                { "@p_city", user.City },
                { "@p_firstName", user.FirstName },
                { "@p_lastName", user.LastName },
                { "@p_phoneNumber", user.PhoneNumber },
                { "@p_country", user.Country },
                { "@p_gender", user.Gender }
            };

            await MySqlDatabaseService.Instance.ExecuteStoredProcedureAsync("CreateUser", parameters);
        }

        public async Task<bool> IsUserVerified(string salt)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@p_salt", salt }
            };

            var response = await MySqlDatabaseService.Instance.ExecuteQueryAsync("SELECT isVerified FROM users WHERE salt = @p_salt", parameters);
            return response.Count > 0 && (sbyte)response[0]["isVerified"] == 1;
        }
    }
}
