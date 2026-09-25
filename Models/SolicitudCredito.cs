using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlataformaCreditos.Models;

public static class EstadosSolicitud
{
    public const string Pendiente = "Pendiente";
    public const string Aprobado = "Aprobado";
    public const string Rechazado = "Rechazado";

    public static readonly string[] Todos = [Pendiente, Aprobado, Rechazado];
}

public class SolicitudCredito
{
    public int Id { get; set; }

    [Required]
    public int ClienteId { get; set; }

    public Cliente? Cliente { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "El monto solicitado debe ser mayor a 0.")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal MontoSolicitado { get; set; }

    public DateTime FechaSolicitud { get; set; } = DateTime.UtcNow;

    [Required]
    [MaxLength(20)]
    public string Estado { get; set; } = EstadosSolicitud.Pendiente;

    [MaxLength(500)]
    public string? MotivoRechazo { get; set; }
}
