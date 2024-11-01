using MySql.Data.MySqlClient;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add MySQL connection dependency to DI container
string connectionString = "server=localhost;port=3005;user=stefan;password=stefan2002;database=testdb;";
builder.Services.AddTransient<MySqlConnection>(_ => new MySqlConnection(connectionString));

// Configure CORS to allow requests only from localhost
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalhost", policy =>
        policy.WithOrigins("http://localhost:5000")
              .AllowAnyMethod()
              .AllowAnyHeader());
});

// Configure the .NET application to listen on localhost only
builder.WebHost.UseUrls("http://localhost:5000");

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Enable CORS policy for localhost
app.UseCors("AllowLocalhost");

// Optional: Enable HTTPS redirection if needed in production
app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
