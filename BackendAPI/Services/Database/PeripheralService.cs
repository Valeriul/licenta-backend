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

        public async Task<bool> InitializePeripheral(ulong id_user)
        {
            var result = await CommunicationManager.Instance.HandleCommand(new CommandRequest
            {
                CommandType = "get_all_peripherals",
                id_user = id_user,
            });

            result = result.Replace("[\"", "[").Replace("\"]", "]").Replace("\\\"", "\"").Replace("[[", "[").Replace("]]", "]");
            var peripherals = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(result);

            foreach (var peripheral in peripherals)
            {
                try
                {
                    await AddPeripheral(id_user, peripheral["uuid"].ToString(), peripheral["type"].ToString());
                }
                catch (System.Exception e)
                {
                    System.Console.WriteLine(e.Message);
                }
            }

            return true;
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
            try
            {
                // Attempt to get data from external sources.
                string allPeripheralsJson = string.Empty;
                string allDataJson = string.Empty;
                try
                {
                    allPeripheralsJson = await GetAllPeripherals(id_user);
                    allDataJson = await GetAllData(id_user);
                }
                catch (Exception ex)
                {
                    // Log error here if needed.
                    throw new Exception("Error fetching peripherals or data.", ex);
                }

                // If no peripherals, attempt to initialize and re-fetch.
                if (allPeripheralsJson == "[]")
                {
                    try
                    {
                        await InitializePeripheral(id_user);
                        allPeripheralsJson = await GetAllPeripherals(id_user);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Error initializing peripherals.", ex);
                    }
                }

                // Validate that JSON strings are not null or empty.
                if (string.IsNullOrWhiteSpace(allPeripheralsJson) ||
                    string.IsNullOrWhiteSpace(allDataJson))
                {
                    throw new Exception("One or more JSON responses were empty.");
                }

                // Deserialize peripherals.
                List<Dictionary<string, object>> peripherals;
                try
                {
                    peripherals = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(allPeripheralsJson);
                }
                catch (Exception ex)
                {
                    throw new Exception("Failed to deserialize peripherals JSON.", ex);
                }

                // Deserialize allDataJson into a list of strings.
                List<string> rawData;
                try
                {
                    rawData = JsonConvert.DeserializeObject<List<string>>(allDataJson);
                }
                catch (Exception ex)
                {
                    throw new Exception("Failed to deserialize data JSON into list of strings.", ex);
                }

                if (rawData == null || rawData.Count == 0)
                {
                    throw new Exception("No raw data available after deserialization.");
                }

                // Deserialize the first element of rawData into a list of dictionaries.
                List<Dictionary<string, object>> data;
                try
                {
                    data = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(rawData[0]);
                }
                catch (Exception ex)
                {
                    throw new Exception("Failed to deserialize raw data element.", ex);
                }

                // Ensure our deserialized data is valid.
                if (peripherals == null)
                {
                    throw new Exception("Peripheral data is null after deserialization.");
                }
                if (data == null)
                {
                    throw new Exception("Data is null after deserialization.");
                }

                // Process each peripheral and match data by UUID.
                foreach (var peripheral in peripherals)
                {
                    // Ensure the key exists.
                    if (!peripheral.ContainsKey("uuid_Peripheral"))
                    {
                        peripheral["data"] = null;
                        continue;
                    }

                    string peripheralUuid = peripheral["uuid_Peripheral"]?.ToString();
                    if (string.IsNullOrEmpty(peripheralUuid))
                    {
                        peripheral["data"] = null;
                        continue;
                    }

                    var matchingData = data.FirstOrDefault(d =>
                    {
                        // Check that the key exists and matches.
                        return d.ContainsKey("uuid") &&
                               d["uuid"]?.ToString() == peripheralUuid;
                    });

                    if (matchingData != null)
                    {
                        // If there is a "data" field, attempt to deserialize it.
                        if (matchingData.ContainsKey("data") && matchingData["data"] != null)
                        {
                            try
                            {
                                peripheral["data"] = JsonConvert.DeserializeObject(
                                    matchingData["data"].ToString() ?? "{}"
                                );
                            }
                            catch (Exception ex)
                            {
                                // Log or handle the error for this particular peripheral.
                                peripheral["data"] = null;
                            }
                        }
                        else
                        {
                            peripheral["data"] = null;
                        }
                    }
                    else
                    {
                        peripheral["data"] = null;
                    }
                }

                // Finally, serialize the peripherals back to JSON.
                try
                {
                    return JsonConvert.SerializeObject(peripherals);
                }
                catch (Exception ex)
                {
                    throw new Exception("Failed to serialize the final peripherals list.", ex);
                }
            }
            catch (Exception ex)
            {
                // Global catch-all. In production you might log this exception.
                // You can also choose to re-throw it instead of returning an error string.
                return $"{{\"error\":\"{ex.Message}\"}}";
            }
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

        public async Task<bool> AddPeripheral(ulong id_user, string uuid, string type)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@p_id_user", id_user.ToString() },
                { "@p_uuid", uuid },
                { "@p_type", type }
            };

            await MySqlDatabaseService.Instance.ExecuteNonQueryAsync("INSERT INTO peripherals (uuid_Peripheral, type, uuid_Central) VALUES (@p_uuid, @p_type,(SELECT uuid_Central FROM users where id_user = @p_id_user));", parameters);
            return true;
        }

        public async Task<bool> RemovePeripheral(ulong id_user, string uuid)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@p_id_user", id_user.ToString() },
                { "@p_uuid", uuid }
            };

            await MySqlDatabaseService.Instance.ExecuteNonQueryAsync("DELETE FROM peripherals WHERE uuid_Peripheral = @p_uuid AND uuid_Central = (SELECT uuid_Central FROM users where id_user = @p_id_user);", parameters);
            return true;
        }
    }
}
