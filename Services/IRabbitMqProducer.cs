using PlataformaCreditos.Models;

namespace PlataformaCreditos.Services;

public interface IRabbitMqProducer
{
    Task<(bool Success, string? ErrorMessage)> PublishSolicitudRegistradaAsync(SolicitudRegistradaMensaje mensaje);
}
