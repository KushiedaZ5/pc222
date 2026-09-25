using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Data;
using PlataformaCreditos.Models;
using PlataformaCreditos.Models.ViewModels;
using PlataformaCreditos.Services;

namespace PlataformaCreditos.Controllers;

[Authorize]
public class SolicitudesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ISolicitudCacheService _cacheService;
    private readonly IRabbitMqProducer _rabbitMqProducer;
    private readonly ILogger<SolicitudesController> _logger;

    public SolicitudesController(
        ApplicationDbContext context,
        UserManager<IdentityUser> userManager,
        ISolicitudCacheService cacheService,
        IRabbitMqProducer rabbitMqProducer,
        ILogger<SolicitudesController> logger)
    {
        _context = context;
        _userManager = userManager;
        _cacheService = cacheService;
        _rabbitMqProducer = rabbitMqProducer;
        _logger = logger;
    }

    // GET: /Solicitudes/ o /Solicitudes/MisSolicitudes
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] FiltroSolicitudesViewModel filtro)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        var cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.UsuarioId == user.Id);
        if (cliente == null)
        {
            // Si el usuario es nuevo, creamos un registro de Cliente con valores por defecto
            cliente = new Cliente
            {
                UsuarioId = user.Id,
                IngresosMensuales = 2500m,
                Activo = true
            };
            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();
        }

        // Validaciones server-side de filtros (Pregunta 2)
        if (filtro.MontoMin < 0)
        {
            ModelState.AddModelError(nameof(filtro.MontoMin), "No se aceptan montos mínimos negativos.");
        }
        if (filtro.MontoMax < 0)
        {
            ModelState.AddModelError(nameof(filtro.MontoMax), "No se aceptan montos máximos negativos.");
        }
        if (filtro.MontoMin.HasValue && filtro.MontoMax.HasValue && filtro.MontoMin > filtro.MontoMax)
        {
            ModelState.AddModelError(nameof(filtro.MontoMin), "El monto mínimo no puede ser mayor al monto máximo.");
        }
        if (filtro.FechaInicio.HasValue && filtro.FechaFin.HasValue && filtro.FechaInicio > filtro.FechaFin)
        {
            ModelState.AddModelError(nameof(filtro.FechaInicio), "Rango de fechas inválido: La fecha de inicio no puede ser mayor a la fecha de fin.");
        }

        // Si hay errores de validación de filtros en servidor, regresamos con el mensaje
        if (!ModelState.IsValid)
        {
            filtro.Solicitudes = new List<SolicitudCredito>();
            return View(filtro);
        }

        bool tieneFiltrosActivos = !string.IsNullOrWhiteSpace(filtro.Estado) ||
                                  filtro.MontoMin.HasValue ||
                                  filtro.MontoMax.HasValue ||
                                  filtro.FechaInicio.HasValue ||
                                  filtro.FechaFin.HasValue;

        List<SolicitudCredito>? lista = null;

        // Pregunta 4: Usar Caché de Redis por 60s si no hay filtros activos
        if (!tieneFiltrosActivos)
        {
            lista = await _cacheService.GetCachedSolicitudesAsync(user.Id);
            if (lista != null)
            {
                _logger.LogInformation("Solicitudes obtenidas desde Redis Cache para usuario {UserId}", user.Id);
                filtro.Solicitudes = lista;
                return View(filtro);
            }
        }

        // Consulta a base de datos SQLite
        var query = _context.SolicitudesCredito
            .Where(s => s.ClienteId == cliente.Id);

        if (!string.IsNullOrWhiteSpace(filtro.Estado))
        {
            query = query.Where(s => s.Estado == filtro.Estado);
        }

        if (filtro.MontoMin.HasValue)
        {
            query = query.Where(s => s.MontoSolicitado >= filtro.MontoMin.Value);
        }

        if (filtro.MontoMax.HasValue)
        {
            query = query.Where(s => s.MontoSolicitado <= filtro.MontoMax.Value);
        }

        if (filtro.FechaInicio.HasValue)
        {
            var inicioUtc = filtro.FechaInicio.Value.Date;
            query = query.Where(s => s.FechaSolicitud >= inicioUtc);
        }

        if (filtro.FechaFin.HasValue)
        {
            var finUtc = filtro.FechaFin.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(s => s.FechaSolicitud <= finUtc);
        }

        lista = await query.OrderByDescending(s => s.FechaSolicitud).ToListAsync();

        // Si fue una consulta sin filtros, almacenamos en Redis Cache por 60s
        if (!tieneFiltrosActivos)
        {
            await _cacheService.SetCachedSolicitudesAsync(user.Id, lista);
        }

        filtro.Solicitudes = lista;
        return View(filtro);
    }

    // Alias para la ruta "MisSolicitudes"
    [HttpGet]
    public Task<IActionResult> MisSolicitudes([FromQuery] FiltroSolicitudesViewModel filtro) => Index(filtro);

    // GET: /Solicitudes/Detalle/{id}
    [HttpGet]
    public async Task<IActionResult> Detalle(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var solicitud = await _context.SolicitudesCredito
            .Include(s => s.Cliente)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (solicitud == null)
        {
            return NotFound("La solicitud no existe.");
        }

        // Si el usuario no es Analista y no es el dueño, denegar acceso
        bool esAnalista = User.IsInRole("Analista");
        if (!esAnalista && solicitud.Cliente?.UsuarioId != user.Id)
        {
            return Forbid();
        }

        // Pregunta 4: Guardar en sesión Redis-backed la última solicitud visitada
        HttpContext.Session.SetInt32("UltimaSolicitudId", solicitud.Id);
        HttpContext.Session.SetString("UltimaSolicitudMonto", solicitud.MontoSolicitado.ToString("C"));

        return View(solicitud);
    }

    // GET: /Solicitudes/Crear
    [HttpGet]
    public async Task<IActionResult> Crear()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.UsuarioId == user.Id);
        if (cliente == null)
        {
            cliente = new Cliente
            {
                UsuarioId = user.Id,
                IngresosMensuales = 3000m,
                Activo = true
            };
            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();
        }

        bool tienePendiente = await _context.SolicitudesCredito
            .AnyAsync(s => s.ClienteId == cliente.Id && s.Estado == EstadosSolicitud.Pendiente);

        var model = new CrearSolicitudViewModel
        {
            IngresosMensuales = cliente.IngresosMensuales,
            TienePendiente = tienePendiente,
            ClienteActivo = cliente.Activo
        };

        return View(model);
    }

    // POST: /Solicitudes/Crear
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(CrearSolicitudViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.UsuarioId == user.Id);
        if (cliente == null)
        {
            cliente = new Cliente
            {
                UsuarioId = user.Id,
                IngresosMensuales = 3000m,
                Activo = true
            };
            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();
        }

        model.IngresosMensuales = cliente.IngresosMensuales;
        model.ClienteActivo = cliente.Activo;

        // Validaciones server-side de Pregunta 1 y 3:
        // 1. Cliente debe estar activo
        if (!cliente.Activo)
        {
            ModelState.AddModelError(string.Empty, "Su cuenta de cliente se encuentra inactiva. No puede registrar solicitudes de crédito.");
        }

        // 2. No permitir más de una solicitud Pendiente por cliente
        bool tienePendiente = await _context.SolicitudesCredito
            .AnyAsync(s => s.ClienteId == cliente.Id && s.Estado == EstadosSolicitud.Pendiente);

        if (tienePendiente)
        {
            model.TienePendiente = true;
            ModelState.AddModelError(string.Empty, "Ya tiene una solicitud en estado Pendiente. Debe esperar la evaluación antes de solicitar otra.");
        }

        // 3. MontoSolicitado > 0
        if (model.MontoSolicitado <= 0)
        {
            ModelState.AddModelError(nameof(model.MontoSolicitado), "El monto solicitado debe ser mayor a 0.");
        }

        // 4. El monto solicitado no puede superar 10 veces los ingresos mensuales
        decimal limiteMaximo = cliente.IngresosMensuales * 10m;
        if (model.MontoSolicitado > limiteMaximo)
        {
            ModelState.AddModelError(nameof(model.MontoSolicitado), 
                $"El monto solicitado ({model.MontoSolicitado:C}) supera el límite de 10 veces sus ingresos mensuales ({limiteMaximo:C}).");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Persistir en SQLite en estado Pendiente
        var solicitud = new SolicitudCredito
        {
            ClienteId = cliente.Id,
            MontoSolicitado = model.MontoSolicitado,
            FechaSolicitud = DateTime.UtcNow,
            Estado = EstadosSolicitud.Pendiente
        };

        try
        {
            _context.SolicitudesCredito.Add(solicitud);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Error al persistir la solicitud en SQLite.");
            ModelState.AddModelError(string.Empty, "Error al registrar la solicitud en base de datos. Verifique las restricciones.");
            return View(model);
        }

        // Invalidar caché Redis del usuario (Pregunta 4)
        await _cacheService.InvalidateUserCacheAsync(user.Id);

        // Pregunta 7: Publicar mensaje JSON persistente SolicitudRegistrada en Cloud MQ
        var mensajeCola = new SolicitudRegistradaMensaje
        {
            MessageId = Guid.NewGuid().ToString(),
            SolicitudId = solicitud.Id,
            UsuarioId = user.Id,
            FechaEventoUtc = DateTime.UtcNow
        };

        var (publishOk, publishErr) = await _rabbitMqProducer.PublishSolicitudRegistradaAsync(mensajeCola);

        if (!publishOk)
        {
            _logger.LogWarning("Fallo al publicar mensaje en Cloud MQ: {Error}", publishErr);
            ViewBag.RabbitMqWarning = $"La solicitud #{solicitud.Id} se guardó exitosamente, pero la notificación no pudo encolarse en Cloud MQ ({publishErr}). MessageId: {mensajeCola.MessageId}.";
        }
        else
        {
            ViewBag.RabbitMqSuccess = $"Notificación encolada correctamente en Cloud MQ (MessageId: {mensajeCola.MessageId}).";
        }

        ViewBag.SuccessMessage = $"¡Solicitud #{solicitud.Id} registrada exitosamente por {solicitud.MontoSolicitado:C} en estado Pendiente!";
        model.TienePendiente = true;

        return View(model);
    }

    // Endpoint para sincronización por WebSocket tras reconexión (Pregunta 6)
    [HttpGet]
    public async Task<IActionResult> ObtenerEstado(int id)
    {
        var solicitud = await _context.SolicitudesCredito
            .Select(s => new { s.Id, s.Estado, s.MotivoRechazo, s.MontoSolicitado, s.FechaSolicitud })
            .FirstOrDefaultAsync(s => s.Id == id);

        if (solicitud == null) return NotFound();
        return Json(solicitud);
    }
}
