using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using System.Collections.Generic;

[ApiController]
[Route("api/[controller]")]
public class DataController : ControllerBase
{
    private readonly MySqlConnection _connection;

    public DataController(MySqlConnection connection)
    {
        _connection = connection;
    }

    [HttpGet]
    public IActionResult GetData()
    {
        var result = new List<dynamic>();

        _connection.Open();

        // Create and populate a temporary table if needed
        var createTableCommand = new MySqlCommand(@"
            CREATE TEMPORARY TABLE IF NOT EXISTS temp_data (
                id INT AUTO_INCREMENT PRIMARY KEY,
                name VARCHAR(100),
                age INT
            );", _connection);
        createTableCommand.ExecuteNonQuery();

        var insertDataCommand = new MySqlCommand(@"
            INSERT INTO temp_data (name, age) VALUES ('Alice', 25), ('Bob', 30);", _connection);
        insertDataCommand.ExecuteNonQuery();

        var selectCommand = new MySqlCommand("SELECT * FROM temp_data;", _connection);
        var reader = selectCommand.ExecuteReader();

        while (reader.Read())
        {
            result.Add(new { Id = reader["id"], Name = reader["name"], Age = reader["age"] });
        }

        _connection.Close();
        return Ok(result);
    }
}

