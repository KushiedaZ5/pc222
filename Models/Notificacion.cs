using System.ComponentModel.DataAnnotations;

namespace PlataformaCreditos.Models;

public class Notificacion
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string MessageId { get; set; } = string.Empty;

    [Required]
    public int SolicitudId { get; set; }

    [Required]
    [MaxLength(100)]
    public string UsuarioId { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string Texto { get; set; } = string.Empty;

    public DateTime FechaProcesamientoUtc { get; set; } = DateTime.UtcNow;
}
