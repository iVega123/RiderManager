using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using System.Text;
using RiderManager.Configurations;

namespace RiderManager.Services.RabbitMQService
{

    public class MessagingConsumerService : IMessagingConsumerService, IDisposable
    {
        private readonly IModel _channel;
        private readonly ILogger<MessagingConsumerService> _logger;
        private readonly string _queueName;

        public MessagingConsumerService(IConnection connection, ILogger<MessagingConsumerService> logger, RabbitMQOptions options)
        {
            _logger = logger;
            _queueName = options.QueueName;

            _channel = connection.CreateModel();

            InitializeQueue();
        }

        private void InitializeQueue()
        {
            _channel.QueueDeclare(queue: _queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
            _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
        }

        public void StartConsuming()
        {
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                try
                {
                    await ProcessMessage(message);
                    _channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _channel.BasicNack(ea.DeliveryTag, false, true);
                    _logger.LogError($"Error processing message: {ex.Message}", ex);
                }
            };

            _channel.BasicConsume(queue: _queueName, autoAck: false, consumer: consumer);
            _logger.LogInformation("Started consuming messages.");
        }

        private Task ProcessMessage(string message)
        {
            _logger.LogInformation($"Received message: {message}");
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _channel?.Close();
            _channel?.Dispose();
            _logger.LogInformation("RabbitMQ channel closed.");
        }
    }
}
