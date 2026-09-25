using System.ComponentModel.DataAnnotations;
using PlataformaCreditos.Models;

namespace PlataformaCreditos.Models.ViewModels;

public class FiltroSolicitudesViewModel
{
    public string? Estado { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "El monto mínimo no puede ser negativo.")]
    public decimal? MontoMin { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "El monto máximo no puede ser negativo.")]
    public decimal? MontoMax { get; set; }

    [DataType(DataType.Date)]
    public DateTime? FechaInicio { get; set; }

    [DataType(DataType.Date)]
    public DateTime? FechaFin { get; set; }

    public List<SolicitudCredito> Solicitudes { get; set; } = new();

    public string? ErrorMessage { get; set; }
}

public class CrearSolicitudViewModel
{
    [Required(ErrorMessage = "El monto solicitado es obligatorio.")]
    [Range(0.01, double.MaxValue, ErrorMessage = "El monto solicitado debe ser mayor a 0.")]
    [Display(Name = "Monto Solicitado")]
    public decimal MontoSolicitado { get; set; }

    [Display(Name = "Ingresos Mensuales Registrados")]
    public decimal IngresosMensuales { get; set; }

    [Display(Name = "Capacidad Máxima (10x Ingresos)")]
    public decimal CapacidadMaxima => IngresosMensuales * 10;

    public bool TienePendiente { get; set; }
    public bool ClienteActivo { get; set; } = true;
}

public class RechazarSolicitudInputModel
{
    [Required]
    public int SolicitudId { get; set; }

    [Required(ErrorMessage = "El motivo de rechazo es obligatorio.")]
    [StringLength(500, MinimumLength = 5, ErrorMessage = "El motivo debe tener entre 5 y 500 caracteres.")]
    [Display(Name = "Motivo del Rechazo")]
    public string MotivoRechazo { get; set; } = string.Empty;
}
