using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace BackendAPI.Services
{
    public class MySqlDatabaseService
    {
        private readonly string _mySqlConnectionString;
        private static MySqlDatabaseService? _instance;
        private MySqlDatabaseService(IConfiguration configuration)
        {
            _mySqlConnectionString = configuration.GetConnectionString("MySqlConnection")
                ?? throw new InvalidOperationException("Connection string 'MySqlConnection' is missing.");
        }

        public static void Initialize(IConfiguration configuration)
        {
            if (_instance == null)
            {
                _instance = new MySqlDatabaseService(configuration);
            }
        }

        public static MySqlDatabaseService Instance
        {
            get
            {
                if (_instance == null)
                {
                    throw new InvalidOperationException("MySqlDatabaseService must be initialized before use.");
                }
                return _instance;
            }
        }

        private MySqlConnection GetConnection() => new MySqlConnection(_mySqlConnectionString);

        
        public async Task<object?> ExecuteStoredProcedureAsync(string procedureName, Dictionary<string, object>? parameters = null)
        {
            await using var connection = GetConnection();
            await connection.OpenAsync();

            await using var command = new MySqlCommand(procedureName, connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            
            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    command.Parameters.AddWithValue(param.Key, param.Value);
                }
            }

            return await command.ExecuteScalarAsync();
        }



        
        public async Task<List<Dictionary<string, object>>> ExecuteQueryAsync(string query, Dictionary<string, object>? parameters = null)
        {
            var results = new List<Dictionary<string, object>>();

            await using var connection = GetConnection();
            await connection.OpenAsync();

            await using var command = new MySqlCommand(query, connection);
            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    command.Parameters.AddWithValue(param.Key, param.Value);
                }
            }

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.GetValue(i);
                }
                results.Add(row);
            }
            return results;
        }

        
        public async Task<int> ExecuteNonQueryAsync(string query, Dictionary<string, object>? parameters = null)
        {
            await using var connection = GetConnection();
            await connection.OpenAsync();

            await using var command = new MySqlCommand(query, connection);
            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    command.Parameters.AddWithValue(param.Key, param.Value);
                }
            }

            return await command.ExecuteNonQueryAsync();
        }

    }
}
