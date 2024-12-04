using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RasberryAPI.Middlewares;
using RasberryAPI.Peripherals;

namespace RasberryAPI
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            
            services.AddControllers();
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();

        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UsePathBase("/rasberry");
            app.UseDeveloperExceptionPage();

            
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                // Use the /rasberry prefix for Swagger endpoint
                c.SwaggerEndpoint("/rasberry/swagger/v1/swagger.json", "WebSocket Server API v1");
                c.RoutePrefix = string.Empty; // Serve Swagger UI at the root of /rasberry
            });



            
            PeripheralManager.Instance.InitializeFromJson();

            
            app.UseWebSockets();
            app.UseMiddleware<WebSocketMiddleware>();

            
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}