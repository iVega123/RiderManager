using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using System.Text;
using RiderManager.Configurations;
using System.Collections.Concurrent;
using RiderManager.Entities;
using System.Text.Json;
using RiderManager.Managers;
using RiderManager.DTOs;

namespace RiderManager.Services.RabbitMQService
{
    public class MessagingConsumerService : IMessagingConsumerService, IDisposable
    {
        private readonly IModel _channel;
        private readonly ILogger<MessagingConsumerService> _logger;
        private readonly IConnection _connection;
        private readonly string _riderInfoQueueName;
        private readonly string _imageStreamQueueName;
        private readonly IServiceProvider _serviceProvider;
        private ConcurrentDictionary<string, List<ImagePart>> imagePartsStore = new ConcurrentDictionary<string, List<ImagePart>>();

        public MessagingConsumerService(IRabbitMqService mqService,
            ILogger<MessagingConsumerService> logger, 
            RabbitMQOptions options,
            IServiceProvider serviceProvider)
        {
            _connection = mqService.CreateChannel();
            _logger = logger;
            _riderInfoQueueName = options.RiderInfoQueueName;
            _imageStreamQueueName = options.ImageStreamQueueName;
            _channel = _connection.CreateModel();
            InitializeQueues();
            _serviceProvider = serviceProvider;
        }

        private void InitializeQueues()
        {
            _channel.QueueDeclare(queue: _riderInfoQueueName, durable: false, exclusive: false);
            _channel.QueueDeclare(queue: _imageStreamQueueName, durable: false, exclusive: false);
            _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
        }

        public async Task StartConsuming()
        {
            ConsumeQueueAsync(_riderInfoQueueName, ProcessRiderInfo);
            ConsumeQueueAsync(_imageStreamQueueName, ProcessImageStream);
            await Task.CompletedTask;
        }

        private void ConsumeQueueAsync(string queueName, Func<string, Task> processMessageFunc)
        {
            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                try
                {
                    await processMessageFunc(message);
                    _channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _channel.BasicNack(ea.DeliveryTag, false, true);
                    _logger.LogError($"Error processing message: {ex.Message}", ex);
                }
            };

            _channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);
            _logger.LogInformation($"Started consuming messages from {queueName}.");
        }


        private async Task ProcessRiderInfo(string message)
        {
            var riderInfo = JsonSerializer.Deserialize<RiderMQEntity>(message);
            using (var scope = _serviceProvider.CreateScope())
            {
                var riderManager = scope.ServiceProvider.GetRequiredService<IRiderManager>();
                await riderManager.AddRiderAsync(new RiderDTO
                {
                    UserId = riderInfo.UserId,
                    Name = riderInfo.Email,
                    CNPJ = riderInfo.CNPJ,
                    DateOfBirth = riderInfo.DataNascimento,
                    CNHNumber = riderInfo.CNHNumber,
                    CNHType = riderInfo.CNHType
                });
            }
        }


        private async Task ProcessImageStream(string message)
        {
            var imagePart = JsonSerializer.Deserialize<ImagePart>(message);
            List<ImagePart> parts = imagePartsStore.GetOrAdd(imagePart.UserId, new List<ImagePart>());
            parts.Add(imagePart);

            if (imagePart.EndOfFile)
            {
                parts.Sort((x, y) => x.SequenceNumber.CompareTo(y.SequenceNumber));

                using (var memoryStream = new MemoryStream())
                {
                    foreach (var part in parts)
                    {
                        memoryStream.Write(part.Content, 0, part.Content.Length);
                    }

                    memoryStream.Position = 0;
                    await StoreImage(memoryStream, imagePart.FileName, imagePart.UserId);
                }

                imagePartsStore.TryRemove(imagePart.UserId, out _);
            }
        }

        private async Task StoreImage(MemoryStream imageStream, string fileName, string userId)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var riderManager = scope.ServiceProvider.GetRequiredService<IRiderManager>();
                var toFormFile = ConvertToIFormFile(imageStream, fileName);
                await riderManager.UpdateRiderImageAsync(userId, toFormFile);
            }
        }

        private IFormFile ConvertToIFormFile(MemoryStream memoryStream, string fileName)
        {
            memoryStream.Position = 0;

            IFormFile formFile = new FormFile(memoryStream, 0, memoryStream.Length, "name", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = "application/octet-stream"
            };

            return formFile;
        }

        public void Dispose()
        {
            _channel?.Close();
            _channel?.Dispose();
            _logger.LogInformation("RabbitMQ channel closed.");
        }
    }
}
