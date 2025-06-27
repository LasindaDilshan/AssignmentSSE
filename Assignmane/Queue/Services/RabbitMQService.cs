using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace Assignmane.Queue.Services
{
    public class RabbitMQService
    {
        private readonly string _hostName;

        public RabbitMQService(string hostName)
        {
            _hostName = hostName;
        }

        // ❌ Use of 'async void' is discouraged — refactor to 'Task'
        public async void PublishMessage<T>(string queueName, T message)
        {
            var factory = new ConnectionFactory
            {
                // ❌ Hardcoded values — refactor to use IConfiguration (e.g., appsettings.json)
                HostName = "localhost", // ❌ Should use _hostName instead
                Port = 5672,
                UserName = "admin",
                Password = "admin"
            };

            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

            // ✅ Basic publish to default exchange using routingKey = queueName
            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: queueName,
                body: body
            );

            Console.WriteLine($" [x] Sent to '{queueName}': {JsonSerializer.Serialize(message)}");
        }
    }
}
