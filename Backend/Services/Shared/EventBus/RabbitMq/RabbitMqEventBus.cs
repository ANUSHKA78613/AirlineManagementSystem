using System.Text;
using System.Text.Json;
using EventBus.Abstractions;
using EventBus.Events;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace EventBus.RabbitMq
{
    public class RabbitMqEventBus : IEventBus, IDisposable
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly IServiceProvider? _serviceProvider;
        private const string ExchangeName = "airline_event_bus";

        public RabbitMqEventBus(string hostName = "localhost", IServiceProvider? serviceProvider = null)
        {
            _serviceProvider = serviceProvider;
            var factory = new ConnectionFactory { HostName = hostName };

            // Retry connection up to 10 times with 5s delay
            for (int attempt = 1; attempt <= 10; attempt++)
            {
                try
                {
                    _connection = factory.CreateConnection();
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EventBus] RabbitMQ connection attempt {attempt}/10 failed: {ex.Message}");
                    if (attempt == 10) throw;
                    Thread.Sleep(5000);
                }
            }

            _channel = _connection!.CreateModel();
            _channel.ExchangeDeclare(ExchangeName, ExchangeType.Direct, durable: true);
        }

        public void Publish<T>(T @event) where T : IntegrationEvent
        {
            var eventName = typeof(T).Name;
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(@event));
            _channel.BasicPublish(exchange: ExchangeName, routingKey: eventName, body: body);
            Console.WriteLine($"[EventBus] Published: {eventName}");
        }

        public void Subscribe<T>(Action<T> handler) where T : IntegrationEvent
        {
            var eventName = typeof(T).Name;
            var queueName = $"{eventName}_queue";

            _channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false);
            _channel.QueueBind(queue: queueName, exchange: ExchangeName, routingKey: eventName);

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (model, ea) =>
            {
                var body = Encoding.UTF8.GetString(ea.Body.ToArray());
                var @event = JsonSerializer.Deserialize<T>(body);
                if (@event != null)
                {
                    Console.WriteLine($"[EventBus] Received: {eventName}");
                    handler(@event);
                }
                _channel.BasicAck(ea.DeliveryTag, false);
            };

            _channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);
            Console.WriteLine($"[EventBus] Subscribed to: {eventName}");
        }

        public void Subscribe<TEvent, THandler>()
            where TEvent : IntegrationEvent
            where THandler : IIntegrationEventHandler<TEvent>
        {
            var eventName = typeof(TEvent).Name;
            var handlerName = typeof(THandler).Name;
            var queueName = $"{eventName}_{handlerName}_queue";

            _channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false);
            _channel.QueueBind(queue: queueName, exchange: ExchangeName, routingKey: eventName);

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (model, ea) =>
            {
                try
                {
                    var body = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var @event = JsonSerializer.Deserialize<TEvent>(body);
                    if (@event != null)
                    {
                        Console.WriteLine($"[EventBus] Received: {eventName} -> {handlerName}");

                        if (_serviceProvider != null)
                        {
                            using var scope = _serviceProvider.CreateScope();
                            var handler = scope.ServiceProvider.GetRequiredService<THandler>();
                            handler.Handle(@event).GetAwaiter().GetResult();
                        }
                        else
                        {
                            Console.WriteLine($"[EventBus] WARNING: No service provider — cannot resolve {handlerName}");
                        }
                    }
                    _channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EventBus] ERROR handling {eventName}: {ex.Message}");
                    _channel.BasicNack(ea.DeliveryTag, false, true);
                }
            };

            _channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);
            Console.WriteLine($"[EventBus] Subscribed: {eventName} -> {handlerName}");
        }

        public void Dispose()
        {
            _channel?.Dispose();
            _connection?.Dispose();
        }
    }
}
