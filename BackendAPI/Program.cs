using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using BackendAPI.Services;

public class Program
{
    public static async Task Main(string[] args)
    {
        // Initialize services
        var peripheralService = PeripheralService.Instance;
        var webSocketManager = BackendAPI.Services.WebSocketManager.Instance;

        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddScoped<UserService>();

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowFrontend", policy =>
            {
                policy.WithOrigins("http://localhost:3000")
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials();
            });
        });

        MySqlDatabaseService.Initialize(builder.Configuration);
        await webSocketManager.InitializeAsync();

        var app = builder.Build();

        app.UsePathBase("/api");

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseCors("AllowFrontend");
        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }
}
