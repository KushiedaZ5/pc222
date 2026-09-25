using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PlataformaCreditos.Hubs;

[Authorize]
public class SolicitudesHub : Hub
{
    private readonly ILogger<SolicitudesHub> _logger;

    public SolicitudesHub(ILogger<SolicitudesHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var usuarioId = Context.UserIdentifier;
        _logger.LogInformation("Usuario autenticado conectado al Hub de Solicitudes: {UsuarioId}, ConnectionId: {ConnectionId}", 
            usuarioId, Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var usuarioId = Context.UserIdentifier;
        _logger.LogInformation("Usuario desconectado del Hub de Solicitudes: {UsuarioId}, ConnectionId: {ConnectionId}", 
            usuarioId, Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
