using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Data;

namespace PlataformaCreditos.Controllers;

[Authorize]
public class NotificacionesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;

    public NotificacionesController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // GET: /Notificaciones o /Notificaciones/Index
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        // Obtener únicamente las notificaciones generadas por Cloud MQ pertenecientes al usuario autenticado
        var notificaciones = await _context.Notificaciones
            .Where(n => n.UsuarioId == user.Id)
            .OrderByDescending(n => n.FechaProcesamientoUtc)
            .ToListAsync();

        return View(notificaciones);
    }
}
