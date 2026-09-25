namespace PlataformaCreditos.Services;

public class RabbitMqSettings
{
    public const string SectionName = "RabbitMq";

    public string? ConnectionString { get; set; }
    public string QueueName { get; set; } = "solicitudes.notificaciones";
    public bool ConsumerEnabled { get; set; } = true;
}
