using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlataformaCreditos.Models;

public class Cliente
{
    public int Id { get; set; }

    [Required]
    public string UsuarioId { get; set; } = string.Empty;

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Los ingresos mensuales deben ser mayores a 0.")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal IngresosMensuales { get; set; }

    public bool Activo { get; set; } = true;

    // Relación con solicitudes
    public ICollection<SolicitudCredito> Solicitudes { get; set; } = new List<SolicitudCredito>();
}
