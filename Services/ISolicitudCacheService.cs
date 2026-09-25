using PlataformaCreditos.Models;

namespace PlataformaCreditos.Services;

public interface ISolicitudCacheService
{
    Task<List<SolicitudCredito>?> GetCachedSolicitudesAsync(string usuarioId);
    Task SetCachedSolicitudesAsync(string usuarioId, List<SolicitudCredito> solicitudes, TimeSpan? expiry = null);
    Task InvalidateUserCacheAsync(string usuarioId);
}
