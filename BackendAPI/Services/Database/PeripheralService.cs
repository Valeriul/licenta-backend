using System.Collections.Generic;
using System.Threading.Tasks;
using BackendAPI.Models;
using Newtonsoft.Json;

namespace BackendAPI.Services
{
    public class PeripheralService
    {
        public PeripheralService() { }
        public async Task<string> GetAllData(ulong id_user)
        {

            var result = await CommunicationManager.Instance.HandleCommand(new CommandRequest
            {
                CommandType = "GET_ALL_DATA",
                id_user = id_user,
            });

            return result ?? string.Empty;
        }

        public async Task<object> GetSensorData(ulong id_user)
        {
            var result = await CommunicationManager.Instance.HandleCommand(new CommandRequest
            {
                CommandType = "GET_ALL_SENSOR_DATA",
                id_user = id_user,
            });

            return result ?? string.Empty;
        }

        public async Task<string> GetAllPeripherals(ulong id_user)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@p_id_user", id_user.ToString() }
            };

            var queryResult = await MySqlDatabaseService.Instance.ExecuteQueryAsync("SELECT uuid_Peripheral,type,name,location,grid_position FROM backend_db.peripherals INNER join users using(uuid_Central) where id_user = @p_id_user;", parameters);
            return Newtonsoft.Json.JsonConvert.SerializeObject(queryResult);
        }

        public async Task<string> GetLoadingData(ulong id_user)
        {
            var allPeripheralsJson = await GetAllPeripherals(id_user);
            var allDataJson = await GetAllData(id_user);

            
            var peripherals = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(allPeripheralsJson);

            
            var rawData = JsonConvert.DeserializeObject<List<string>>(allDataJson);

            
            var data = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(rawData[0]);

            
            foreach (var peripheral in peripherals)
            {
                var matchingData = data.FirstOrDefault(d => d["uuid"].ToString() == peripheral["uuid_Peripheral"].ToString());
                if (matchingData != null)
                {
                    
                    peripheral["data"] = matchingData["data"] != null
                        ? JsonConvert.DeserializeObject(matchingData["data"].ToString() ?? "{}")
                        : null;
                }
                else
                {
                    
                    peripheral["data"] = null;
                }
            }

            
            return JsonConvert.SerializeObject(peripherals);
        }
        public async Task<string> ControlPeripheral(ulong id_user, BackendAPI.Models.ControlCommand data)
        {
            var result = await CommunicationManager.Instance.HandleCommand(new CommandRequest
            {
                CommandType = "CONTROL",
                id_user = id_user,
                Data = JsonConvert.SerializeObject(data),
            });

            return result ?? string.Empty;
        }

        public async Task<bool> RenamePeripheral(ulong id_user, string uuid, string newName)
        {

            var parameters = new Dictionary<string, object>
            {
                { "@p_id_user", id_user.ToString() },
                { "@p_uuid", uuid },
                { "@p_newName", newName }
            };

            var queryResult = await MySqlDatabaseService.Instance.ExecuteNonQueryAsync("UPDATE peripherals p JOIN users u ON p.uuid_Central = u.uuid_Central SET p.name = @p_newName WHERE p.uuid_Peripheral = @p_uuid AND u.id_user = @p_id_user;", parameters);
            return true;

        }

        public async Task<bool> RelocatePeripheral(ulong id_user, string uuid, string newLocation)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@p_id_user", id_user.ToString() },
                { "@p_uuid", uuid },
                { "@p_newLocation", newLocation }
            };

            var queryResult = await MySqlDatabaseService.Instance.ExecuteNonQueryAsync("UPDATE peripherals p JOIN users u ON p.uuid_Central = u.uuid_Central SET p.location = @p_newLocation WHERE p.uuid_Peripheral = @p_uuid AND u.id_user = @p_id_user;", parameters);
            return true;
        }


        public async Task<bool> SaveGridPosition(GridPositionUpdateRequest request)
        {

            foreach (var peripheral in request.Peripherals)
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@p_id_user", request.id_user.ToString() },
                    { "@p_uuid", peripheral.Uuid },
                    { "@p_gridPosition", peripheral.Grid_position }
                };

                var queryResult = await MySqlDatabaseService.Instance.ExecuteNonQueryAsync("UPDATE peripherals p JOIN users u ON p.uuid_Central = u.uuid_Central SET p.grid_position = @p_gridPosition WHERE p.uuid_Peripheral = @p_uuid AND u.id_user = @p_id_user;", parameters);     
            }

            return true;

        }
    }
}
