
using Assignmane.BackgroundServices;
using Assignmane.Queue.Services;
using Assignmane.Repository;
using Assignmane.Services.Interfaces;
using Assignmane.Services;

namespace Assignmane
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            // Register RabbitMQService as a singleton.
            builder.Services.AddSingleton(new RabbitMQService("localhost"));

            // Register ChatSessionRepository as a singleton for in-memory state management.
            builder.Services.AddSingleton<ChatSessionRepository>();

            // Register AgentChatCoordinatorService as a hosted service.
            builder.Services.AddHostedService<AgentChatCoordinatorService>();
            builder.Services.AddSingleton<IAgentAvailabilityService, AgentAvailabilityService>();
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", builder =>
                {
                    builder
                        .AllowAnyOrigin()  // Or use .WithOrigins("http://127.0.0.1:5500") if hosted via VSCode/Live Server
                        .AllowAnyMethod()
                        .AllowAnyHeader();
                });
            });

            var app = builder.Build();
            app.UseCors("AllowAll");

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
