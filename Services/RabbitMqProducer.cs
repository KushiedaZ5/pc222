using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PlataformaCreditos.Models;
using RabbitMQ.Client;

namespace PlataformaCreditos.Services;

public class RabbitMqProducer : IRabbitMqProducer
{
    private readonly RabbitMqSettings _settings;
    private readonly ILogger<RabbitMqProducer> _logger;

    public RabbitMqProducer(IOptions<RabbitMqSettings> options, ILogger<RabbitMqProducer> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<(bool Success, string? ErrorMessage)> PublishSolicitudRegistradaAsync(SolicitudRegistradaMensaje mensaje)
    {
        if (string.IsNullOrWhiteSpace(_settings.ConnectionString))
        {
            const string msg = "RabbitMQ ConnectionString no está configurado. La notificación no pudo encolarse.";
            _logger.LogWarning("{Mensaje}. SolicitudId: {SolicitudId}", msg, mensaje.SolicitudId);
            return (false, msg);
        }

        try
        {
            var factory = new ConnectionFactory
            {
                Uri = new Uri(_settings.ConnectionString)
            };

            await using var connection = await factory.CreateConnectionAsync();
            
            // Habilitar confirmaciones de publicador (Publisher Confirmations)
            var channelOptions = new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true
            );
            await using var channel = await connection.CreateChannelAsync(channelOptions);

            // Declarar cola durable
            await channel.QueueDeclareAsync(
                queue: _settings.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            var json = JsonSerializer.Serialize(mensaje);
            var body = Encoding.UTF8.GetBytes(json);

            // Propiedades persistentes
            var props = new BasicProperties
            {
                MessageId = mensaje.MessageId,
                DeliveryMode = DeliveryModes.Persistent,
                ContentType = "application/json",
                Type = "SolicitudRegistrada",
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };

            // Publicación y confirmación
            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: _settings.QueueName,
                mandatory: true,
                basicProperties: props,
                body: body
            );

            _logger.LogInformation("Mensaje publicado exitosamente en RabbitMQ. MessageId: {MessageId}, SolicitudId: {SolicitudId}",
                mensaje.MessageId, mensaje.SolicitudId);

            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al publicar mensaje en RabbitMQ para la solicitud {SolicitudId}. MessageId: {MessageId}",
                mensaje.SolicitudId, mensaje.MessageId);
            return (false, $"Fallo de conexión o confirmación con Cloud MQ: {ex.Message}");
        }
    }
}
