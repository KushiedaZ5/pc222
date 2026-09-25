using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Data;
using PlataformaCreditos.Hubs;
using PlataformaCreditos.Models;
using PlataformaCreditos.Models.ViewModels;
using PlataformaCreditos.Services;

namespace PlataformaCreditos.Controllers;

[Authorize(Roles = "Analista")]
public class AnalistaController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ISolicitudCacheService _cacheService;
    private readonly IHubContext<SolicitudesHub> _hubContext;
    private readonly ILogger<AnalistaController> _logger;

    public AnalistaController(
        ApplicationDbContext context,
        ISolicitudCacheService cacheService,
        IHubContext<SolicitudesHub> hubContext,
        ILogger<AnalistaController> logger)
    {
        _context = context;
        _cacheService = cacheService;
        _hubContext = hubContext;
        _logger = logger;
    }

    // GET: /Analista
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        // Lista de solicitudes en estado Pendiente
        var solicitudes = await _context.SolicitudesCredito
            .Include(s => s.Cliente)
            .Where(s => s.Estado == EstadosSolicitud.Pendiente)
            .OrderBy(s => s.FechaSolicitud)
            .ToListAsync();

        return View(solicitudes);
    }

    // POST: /Analista/Aprobar/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Aprobar(int id)
    {
        var solicitud = await _context.SolicitudesCredito
            .Include(s => s.Cliente)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (solicitud == null)
        {
            TempData["ErrorMessage"] = "La solicitud no fue encontrada.";
            return RedirectToAction(nameof(Index));
        }

        // Validación: No procesar solicitudes ya aprobadas o rechazadas
        if (solicitud.Estado != EstadosSolicitud.Pendiente)
        {
            TempData["ErrorMessage"] = $"La solicitud #{solicitud.Id} ya fue procesada anteriormente con estado '{solicitud.Estado}'.";
            return RedirectToAction(nameof(Index));
        }

        // Validación de negocio: No se puede aprobar si el monto solicitado excede 5 veces los ingresos mensuales
        decimal limiteAprobacion = (solicitud.Cliente?.IngresosMensuales ?? 0) * 5m;
        if (solicitud.MontoSolicitado > limiteAprobacion)
        {
            TempData["ErrorMessage"] = $"No se puede aprobar la solicitud #{solicitud.Id}. El monto ({solicitud.MontoSolicitado:C}) supera el límite máximo de aprobación de 5 veces los ingresos ({limiteAprobacion:C}).";
            return RedirectToAction(nameof(Index));
        }

        // 1. Guardar primero el estado en la base de datos
        solicitud.Estado = EstadosSolicitud.Aprobado;
        solicitud.MotivoRechazo = null;
        await _context.SaveChangesAsync();

        // 2. Invalidar caché Redis del usuario propietario (obtenido directamente desde el servidor)
        var propietarioUsuarioId = solicitud.Cliente!.UsuarioId;
        await _cacheService.InvalidateUserCacheAsync(propietarioUsuarioId);

        // 3. Emitir el evento SolicitudEstadoActualizado mediante WebSocket (SignalR) únicamente al propietario
        await _hubContext.Clients.User(propietarioUsuarioId).SendAsync("SolicitudEstadoActualizado", new
        {
            SolicitudId = solicitud.Id,
            Estado = solicitud.Estado,
            MotivoRechazo = (string?)null
        });

        _logger.LogInformation("Solicitud #{Id} aprobada por analista {Analista}. Notificación WebSocket enviada a usuario {UserId}",
            solicitud.Id, User.Identity?.Name, propietarioUsuarioId);

        TempData["SuccessMessage"] = $"Solicitud #{solicitud.Id} aprobada correctamente y notificada en tiempo real al cliente.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /Analista/Rechazar
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rechazar(RechazarSolicitudInputModel model)
    {
        // Validación: Motivo obligatorio en rechazo
        if (string.IsNullOrWhiteSpace(model.MotivoRechazo))
        {
            TempData["ErrorMessage"] = "El motivo de rechazo es obligatorio para denegar la solicitud.";
            return RedirectToAction(nameof(Index));
        }

        var solicitud = await _context.SolicitudesCredito
            .Include(s => s.Cliente)
            .FirstOrDefaultAsync(s => s.Id == model.SolicitudId);

        if (solicitud == null)
        {
            TempData["ErrorMessage"] = "La solicitud no fue encontrada.";
            return RedirectToAction(nameof(Index));
        }

        // Validación: No procesar solicitudes ya aprobadas o rechazadas
        if (solicitud.Estado != EstadosSolicitud.Pendiente)
        {
            TempData["ErrorMessage"] = $"La solicitud #{solicitud.Id} ya fue procesada anteriormente con estado '{solicitud.Estado}'.";
            return RedirectToAction(nameof(Index));
        }

        // 1. Guardar primero el estado en la base de datos
        solicitud.Estado = EstadosSolicitud.Rechazado;
        solicitud.MotivoRechazo = model.MotivoRechazo.Trim();
        await _context.SaveChangesAsync();

        // 2. Invalidar caché Redis del usuario propietario (obtenido desde el servidor, nunca desde el cliente)
        var propietarioUsuarioId = solicitud.Cliente!.UsuarioId;
        await _cacheService.InvalidateUserCacheAsync(propietarioUsuarioId);

        // 3. Emitir evento SolicitudEstadoActualizado mediante WebSocket únicamente al usuario propietario
        await _hubContext.Clients.User(propietarioUsuarioId).SendAsync("SolicitudEstadoActualizado", new
        {
            SolicitudId = solicitud.Id,
            Estado = solicitud.Estado,
            MotivoRechazo = solicitud.MotivoRechazo
        });

        _logger.LogInformation("Solicitud #{Id} rechazada por analista {Analista}. Notificación WebSocket enviada a usuario {UserId}",
            solicitud.Id, User.Identity?.Name, propietarioUsuarioId);

        TempData["SuccessMessage"] = $"Solicitud #{solicitud.Id} rechazada correctamente con motivo registrado y notificada al cliente.";
        return RedirectToAction(nameof(Index));
    }
}
