using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using BackendAPI.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BackendAPI.Services
{
    public class PeripheralService
    {
        private static readonly Lazy<PeripheralService> _instance = new Lazy<PeripheralService>(() => new PeripheralService());
        public static PeripheralService Instance => _instance.Value;

        private static readonly ConcurrentDictionary<ulong, Dictionary<string, object>> userData = new ConcurrentDictionary<ulong, Dictionary<string, object>>();
        private static readonly object userDataLock = new object();

        private PeripheralService() { }

        public async void HandleConnectionSuccess(ulong id_user)
        {
            Console.WriteLine($"[INFO] Connection success for user {id_user}");

            // Add retry logic for initialization
            const int maxRetries = 3;
            int retryCount = 0;

            while (retryCount < maxRetries)
            {
                try
                {
                    await InitializePeripheral(id_user);
                    break; // Success, exit retry loop
                }
                catch (Exception ex)
                {
                    retryCount++;
                    Console.WriteLine($"[WARNING] InitializePeripheral attempt {retryCount} failed for user {id_user}: {ex.Message}");

                    if (retryCount < maxRetries)
                    {
                        await Task.Delay(2000 * retryCount); // Exponential backoff
                    }
                    else
                    {
                        Console.WriteLine($"[ERROR] Failed to initialize peripherals for user {id_user} after {maxRetries} attempts");
                        return; // Exit if all retries failed
                    }
                }
            }

            // Continue with peripheral data gathering...
            try
            {
                var peripheralsJson = await GetAllPeripherals(id_user);

                if (string.IsNullOrEmpty(peripheralsJson))
                {
                    Console.WriteLine($"[WARNING] No peripherals data received for user {id_user}");
                    return;
                }

                var peripheralsList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(peripheralsJson);

                // Validate deserialization result
                if (peripheralsList == null)
                {
                    Console.WriteLine($"[WARNING] Failed to deserialize peripherals for user {id_user}");
                    peripheralsList = new List<Dictionary<string, object>>();
                }

                peripheralsList.ForEach(peripheral =>
                {
                    if (peripheral != null)
                    {
                        peripheral["data"] = null;
                    }
                });

                lock (userDataLock)
                {
                    // Initialize user data if it doesn't exist
                    if (!userData.ContainsKey(id_user))
                    {
                        userData[id_user] = new Dictionary<string, object>();
                    }

                    userData[id_user]["peripherals"] = peripheralsList;
                    StopGatheringTask(id_user);

                    var cancellationTokenSource = new CancellationTokenSource();
                    userData[id_user]["cancellationToken"] = cancellationTokenSource;

                    // Start data gathering task with better error handling
                    Task.Run(async () =>
                    {
                        while (!cancellationTokenSource.Token.IsCancellationRequested)
                        {
                            try
                            {
                                // Check if user data still exists before gathering
                                if (!userData.ContainsKey(id_user))
                                {
                                    Console.WriteLine($"[INFO] User {id_user} data removed, stopping data gathering");
                                    break;
                                }

                                await GatherData(id_user);
                                await Task.Delay(3000, cancellationTokenSource.Token);
                            }
                            catch (OperationCanceledException)
                            {
                                Console.WriteLine($"[INFO] Data gathering cancelled for user {id_user}");
                                break;
                            }
                            catch (Exception ex) when (!cancellationTokenSource.Token.IsCancellationRequested)
                            {
                                Console.WriteLine($"[ERROR] Error in data gathering for user {id_user}: {ex.Message}");
                                await Task.Delay(5000, cancellationTokenSource.Token); // Wait longer on error
                            }
                        }
                    }, cancellationTokenSource.Token);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error in HandleConnectionSuccess for user {id_user}: {ex.Message}");
            }
        }
        private async Task GatherData(ulong id_user)
        {
            try
            {
                Console.WriteLine("[INFO] Gathering data for user " + id_user);

                // Check if user data exists before proceeding
                if (!userData.ContainsKey(id_user))
                {
                    Console.WriteLine($"[WARNING] User data not found for user {id_user}");
                    return;
                }

                var allDataJson = await GetAllData(id_user);

                // Check if we got valid data
                if (string.IsNullOrEmpty(allDataJson))
                {
                    Console.WriteLine($"[WARNING] No data received for user {id_user}");
                    return;
                }

                var data = new List<Dictionary<string, object>>();

                try
                {
                    var rawData = JsonConvert.DeserializeObject<List<string>>(allDataJson);

                    if (rawData == null || rawData.Count == 0)
                    {
                        Console.WriteLine($"[INFO] No raw data available for user {id_user}, requesting peripherals");
                        await CommunicationManager.Instance.HandleCommand(new CommandRequest
                        {
                            CommandType = "GET_ALL_PERIPHERALS",
                            id_user = id_user,
                        });
                        return;
                    }

                    Console.WriteLine("[INFO] Raw data: " + rawData[0]);

                    // Check if the first element is valid before deserializing
                    if (!string.IsNullOrEmpty(rawData[0]))
                    {
                        data = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(rawData[0])
                               ?? new List<Dictionary<string, object>>();
                    }
                }
                catch (JsonException jsonEx)
                {
                    Console.WriteLine($"[ERROR] JSON deserialization error for user {id_user}: {jsonEx.Message}");
                    return;
                }

                lock (userDataLock)
                {
                    // Double-check user data exists after acquiring lock
                    if (!userData.TryGetValue(id_user, out var user))
                    {
                        Console.WriteLine($"[WARNING] User data removed while processing for user {id_user}");
                        return;
                    }

                    // Safely get peripherals list
                    if (!user.TryGetValue("peripherals", out var peripheralsObj) ||
                        peripheralsObj is not List<Dictionary<string, object>> peripherals)
                    {
                        Console.WriteLine($"[WARNING] Invalid peripherals data for user {id_user}");
                        return;
                    }

                    foreach (var peripheral in peripherals)
                    {
                        if (peripheral == null) continue;

                        // Safely get peripheral UUID
                        if (!peripheral.TryGetValue("uuid_Peripheral", out var peripheralUuidObj) ||
                            peripheralUuidObj == null)
                        {
                            Console.WriteLine($"[WARNING] Peripheral missing UUID for user {id_user}");
                            continue;
                        }

                        string peripheralUuid = peripheralUuidObj.ToString();

                        // Find matching data
                        var matchingData = data.FirstOrDefault(d =>
                            d != null &&
                            d.TryGetValue("uuid", out var dataUuidObj) &&
                            dataUuidObj?.ToString() == peripheralUuid);

                        if (matchingData != null &&
                            matchingData.TryGetValue("data", out var dataObj))
                        {
                            peripheral["data"] = dataObj != null
                                ? JsonConvert.DeserializeObject(dataObj.ToString() ?? "{}")
                                : null;
                        }
                        else
                        {
                            // Set data to null if no matching data found
                            peripheral["data"] = null;
                        }
                    }

                    userData[id_user] = user;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Unexpected error in GatherData for user {id_user}: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
            }
        }

        public async Task<string> GetAggregatedData(ulong id_user, string? uuid = null, string? date_start = null, string? date_end = null, string? type = null)
        {
            var requestData = new
            {
                Data = JsonConvert.SerializeObject(new
                {
                    date_start = date_start,
                    date_end = date_end,
                    type = type
                }),
                CommandType = "GET_AGGREGATED_DATA",
                Uuid = uuid
            };

            var result = await CommunicationManager.Instance.HandleCommand(new CommandRequest
            {
                CommandType = "GET_AGGREGATED_DATA",
                id_user = id_user,
                Data = JsonConvert.SerializeObject(requestData)
            });

            return result ?? string.Empty;
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

            if (result == null || result == string.Empty)
            {
                Console.WriteLine("[ERROR] Failed to get peripherals for user " + id_user);
                return false;
            }

            result = result.Replace("[\"", "[").Replace("\"]", "]").Replace("\\\"", "\"").Replace("[[", "[").Replace("]]", "]");
            var peripherals = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(result);

            if (peripherals == null || peripherals.Count == 0)
            {
                Console.WriteLine("[INFO] No peripherals found for user " + id_user);
                return false;
            }

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
            if (!userData.ContainsKey(id_user))
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

            lock (userDataLock)
            {
                var peripherals = userData[id_user]["peripherals"] as List<Dictionary<string, object>>;
                var peripheral = peripherals.FirstOrDefault(p => p["uuid_Peripheral"].ToString() == uuid);
                if (peripheral != null)
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

            lock (userDataLock)
            {
                var peripherals = userData[id_user]["peripherals"] as List<Dictionary<string, object>>;
                var peripheral = peripherals.FirstOrDefault(p => p["uuid_Peripheral"].ToString() == uuid);
                if (peripheral != null)
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

                lock (userDataLock)
                {
                    var peripherals = userData[request.id_user]["peripherals"] as List<Dictionary<string, object>>;
                    var peripheralData = peripherals.FirstOrDefault(p => p["uuid_Peripheral"].ToString() == peripheral.Uuid);
                    if (peripheralData != null)
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
