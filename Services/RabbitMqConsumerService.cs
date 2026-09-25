using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PlataformaCreditos.Data;
using PlataformaCreditos.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PlataformaCreditos.Services;

public class RabbitMqConsumerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqSettings _settings;
    private readonly ILogger<RabbitMqConsumerService> _logger;

    public RabbitMqConsumerService(
        IServiceScopeFactory scopeFactory,
        IOptions<RabbitMqSettings> options,
        ILogger<RabbitMqConsumerService> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Requerimiento: Desactivar con RabbitMq__ConsumerEnabled=false
        if (!_settings.ConsumerEnabled)
        {
            _logger.LogInformation("RabbitMQ Consumer está desactivado (RabbitMq:ConsumerEnabled = false). No se iniciará la escucha.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_settings.ConnectionString))
        {
            _logger.LogWarning("RabbitMQ ConnectionString no está configurado. El BackgroundService de consumo permanecerá en espera.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    Uri = new Uri(_settings.ConnectionString),
                    AutomaticRecoveryEnabled = true,
                    NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
                };

                _logger.LogInformation("Conectando consumidor a RabbitMQ/CloudAMQP ({Queue})...", _settings.QueueName);
                await using var connection = await factory.CreateConnectionAsync(stoppingToken);
                await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

                // Declarar cola durable
                await channel.QueueDeclareAsync(
                    queue: _settings.QueueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null,
                    cancellationToken: stoppingToken
                );

                // Procesar de a 1 mensaje a la vez
                await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (sender, ea) =>
                {
                    try
                    {
                        var body = ea.Body.ToArray();
                        var json = Encoding.UTF8.GetString(body);
                        _logger.LogInformation("Mensaje recibido en cola {Queue}: {Payload}", _settings.QueueName, json);

                        SolicitudRegistradaMensaje? mensaje = null;
                        try
                        {
                            mensaje = JsonSerializer.Deserialize<SolicitudRegistradaMensaje>(json);
                        }
                        catch (Exception parseEx)
                        {
                            _logger.LogError(parseEx, "Payload JSON inválido recibido. Rechazando sin reencolar para evitar loops.");
                            await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
                            return;
                        }

                        if (mensaje == null || string.IsNullOrWhiteSpace(mensaje.MessageId) || string.IsNullOrWhiteSpace(mensaje.UsuarioId))
                        {
                            _logger.LogWarning("Mensaje incompleto o nulo. Rechazando sin reencolar.");
                            await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
                            return;
                        }

                        using var scope = _scopeFactory.CreateScope();
                        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                        // Control de idempotencia / unicidad de MessageId
                        var yaProcesado = await dbContext.Notificaciones.AnyAsync(n => n.MessageId == mensaje.MessageId);
                        if (yaProcesado)
                        {
                            _logger.LogInformation("MessageId {MessageId} ya fue procesado anteriormente (redelivery). Confirmando con ACK sin duplicar.", mensaje.MessageId);
                            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
                            return;
                        }

                        // Guardar notificación en SQLite
                        var notificacion = new Notificacion
                        {
                            MessageId = mensaje.MessageId,
                            SolicitudId = mensaje.SolicitudId,
                            UsuarioId = mensaje.UsuarioId,
                            Texto = "Recibimos tu solicitud de crédito y está pendiente de evaluación",
                            FechaProcesamientoUtc = DateTime.UtcNow
                        };

                        dbContext.Notificaciones.Add(notificacion);
                        await dbContext.SaveChangesAsync();

                        // Confirmar con ACK manual únicamente tras guardar exitosamente
                        await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
                        _logger.LogInformation("Notificación persistida y ACK enviado exitosamente. SolicitudId: {SolicitudId}, MessageId: {MessageId}",
                            mensaje.SolicitudId, mensaje.MessageId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Fallo al procesar mensaje de solicitud. No se confirmará como exitoso.");
                        // Ante fallo inesperado de BD/sistema, rechazar sin reencolar o dejar pendiente
                        try
                        {
                            await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
                        }
                        catch
                        {
                            // Ignorar error al hacer NACK si el canal se cerró
                        }
                    }
                };

                await channel.BasicConsumeAsync(
                    queue: _settings.QueueName,
                    autoAck: false, // ACK manual obligatorio
                    consumer: consumer,
                    cancellationToken: stoppingToken
                );

                _logger.LogInformation("Consumidor RabbitMQ escuchando en la cola '{Queue}' con ACK manual...", _settings.QueueName);

                // Mantener el servicio activo hasta cancelación
                var tcs = new TaskCompletionSource();
                using (stoppingToken.Register(s => ((TaskCompletionSource)s!).TrySetResult(), tcs))
                {
                    await tcs.Task;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en la conexión del consumidor RabbitMQ. Reintentando en 15 segundos...");
                await Task.Delay(15000, stoppingToken);
            }
        }
    }
}
