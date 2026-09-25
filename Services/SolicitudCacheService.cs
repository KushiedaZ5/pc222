using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Distributed;
using PlataformaCreditos.Models;

namespace PlataformaCreditos.Services;

public class SolicitudCacheService : ISolicitudCacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<SolicitudCacheService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        PropertyNameCaseInsensitive = true
    };

    public SolicitudCacheService(IDistributedCache cache, ILogger<SolicitudCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    private static string GetCacheKey(string usuarioId) => $"solicitudes_usuario_{usuarioId}";

    public async Task<List<SolicitudCredito>?> GetCachedSolicitudesAsync(string usuarioId)
    {
        try
        {
            var key = GetCacheKey(usuarioId);
            var cachedBytes = await _cache.GetAsync(key);
            if (cachedBytes == null || cachedBytes.Length == 0)
            {
                return null;
            }

            return JsonSerializer.Deserialize<List<SolicitudCredito>>(cachedBytes, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al leer desde Redis/Cache para el usuario {UsuarioId}", usuarioId);
            return null;
        }
    }

    public async Task SetCachedSolicitudesAsync(string usuarioId, List<SolicitudCredito> solicitudes, TimeSpan? expiry = null)
    {
        try
        {
            var key = GetCacheKey(usuarioId);
            var options = new DistributedCacheEntryOptions
            {
                // Requerimiento: Cachear por 60s
                AbsoluteExpirationRelativeToNow = expiry ?? TimeSpan.FromSeconds(60)
            };

            var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(solicitudes, JsonOptions);
            await _cache.SetAsync(key, jsonBytes, options);
            _logger.LogInformation("Listado de solicitudes cacheado en Redis por {Segundos}s para usuario {UsuarioId}", 
                (expiry ?? TimeSpan.FromSeconds(60)).TotalSeconds, usuarioId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al escribir en Redis/Cache para el usuario {UsuarioId}", usuarioId);
        }
    }

    public async Task InvalidateUserCacheAsync(string usuarioId)
    {
        try
        {
            var key = GetCacheKey(usuarioId);
            await _cache.RemoveAsync(key);
            _logger.LogInformation("Cache de solicitudes invalidado en Redis para usuario {UsuarioId}", usuarioId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al invalidar cache en Redis para el usuario {UsuarioId}", usuarioId);
        }
    }
}
