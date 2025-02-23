using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using BackendAPI.Models;
using Newtonsoft.Json;

namespace BackendAPI.Services
{
    public class PeripheralService
    {
        private static readonly Lazy<PeripheralService> _instance = new Lazy<PeripheralService>(() => new PeripheralService());
        public static PeripheralService Instance => _instance.Value;

        private static readonly ConcurrentDictionary<ulong, Dictionary<string, object>> userData = new ConcurrentDictionary<ulong, Dictionary<string, object>>();
        private static readonly object userDataLock = new object();

        private PeripheralService(){}
        

        public async void HandleConnectionSuccess(ulong id_user)
        {
            Console.WriteLine("[INFO] Connection success for user " + id_user);
            await InitializePeripheral(id_user);
            var peripheralsJson = await GetAllPeripherals(id_user);
            var peripheralsList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(peripheralsJson);

            peripheralsList.ForEach(peripheral =>
            {
                peripheral.Add("data", null);
            });

            lock (userDataLock)
            {
                // Ensure the userData entry exists
                if (!userData.ContainsKey(id_user))
                {
                    userData[id_user] = new Dictionary<string, object>();
                }

                // Store the peripherals
                userData[id_user]["peripherals"] = peripheralsList;

                // Stop any previous task
                StopGatheringTask(id_user);

                // Create a new cancellation token source
                var cancellationTokenSource = new CancellationTokenSource();
                userData[id_user]["cancellationToken"] = cancellationTokenSource;

                Task.Run(async () =>
                {
                    while (!cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        await GatherData(id_user);
                        await Task.Delay(3000, cancellationTokenSource.Token);
                    }
                }, cancellationTokenSource.Token);
            }
        }

        private async Task GatherData(ulong id_user)
        {
            var allDataJson = await GetAllData(id_user);

            var data = new List<Dictionary<string, object>>();
            try
            {
                var rawData = JsonConvert.DeserializeObject<List<string>>(allDataJson);
                data = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(rawData[0]);
            }
            catch (System.Exception e)
            {
                System.Console.WriteLine(e.Message);
            }

            lock (userDataLock)
            {
                if (!userData.TryGetValue(id_user, out var user))
                {
                    return;
                }

                var peripherals = user["peripherals"] as List<Dictionary<string, object>>;
                foreach (var peripheral in peripherals)
                {
                    var matchingData = data.FirstOrDefault(d => d["uuid"].ToString() == peripheral["uuid_Peripheral"].ToString());
                    if (matchingData != null)
                    {
                        peripheral["data"] = matchingData["data"] != null
                            ? JsonConvert.DeserializeObject(matchingData["data"].ToString() ?? "{}")
                            : null;
                    }
                }

                userData[id_user] = user;
            }
        }

        public void HandleConnectionFailure(ulong id_user)
        {
            Console.WriteLine("[INFO] Connection failure for user " + id_user);
            lock (userDataLock)
            {
                // Stop the running task and remove user entry
                StopGatheringTask(id_user);
                userData.TryRemove(id_user, out _);
            }
        }

        private void StopGatheringTask(ulong id_user)
        {
            if (userData.TryGetValue(id_user, out var user) && user.ContainsKey("cancellationToken"))
            {
                var cancellationTokenSource = user["cancellationToken"] as CancellationTokenSource;
                cancellationTokenSource?.Cancel();
                cancellationTokenSource?.Dispose();
                user.Remove("cancellationToken");
                System.Console.WriteLine($"[INFO] Stopped data-gathering task for user {id_user}");
            }
        }

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
            if(!userData.ContainsKey(id_user))
            {
                WebSocketManager.Instance.TryReconnectWebSocket(id_user);
                return JsonConvert.SerializeObject(new List<Dictionary<string, object>>());
            }

            return await Task.Run(() =>
            {
                var peripherals = userData[id_user]["peripherals"] as List<Dictionary<string, object>>;
                return JsonConvert.SerializeObject(peripherals);
            });
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

            lock(userDataLock)
            {
                var peripherals = userData[id_user]["peripherals"] as List<Dictionary<string, object>>;
                var peripheral = peripherals.FirstOrDefault(p => p["uuid_Peripheral"].ToString() == uuid);
                if(peripheral != null)
                {
                    peripheral["name"] = newName;
                    userData[id_user]["peripherals"] = peripherals;
                }
            }

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

            lock(userDataLock)
            {
                var peripherals = userData[id_user]["peripherals"] as List<Dictionary<string, object>>;
                var peripheral = peripherals.FirstOrDefault(p => p["uuid_Peripheral"].ToString() == uuid);
                if(peripheral != null)
                {
                    peripheral["location"] = newLocation;
                    userData[id_user]["peripherals"] = peripherals;
                }
            }

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

                lock(userDataLock)
                {
                    var peripherals = userData[request.id_user]["peripherals"] as List<Dictionary<string, object>>;
                    var peripheralData = peripherals.FirstOrDefault(p => p["uuid_Peripheral"].ToString() == peripheral.Uuid);
                    if(peripheralData != null)
                    {
                        peripheralData["grid_position"] = peripheral.Grid_position;
                        userData[request.id_user]["peripherals"] = peripherals;
                    }
                }

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
