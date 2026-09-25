using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Models;

namespace PlataformaCreditos.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<SolicitudCredito> SolicitudesCredito => Set<SolicitudCredito>();
    public DbSet<Notificacion> Notificaciones => Set<Notificacion>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Cliente
        builder.Entity<Cliente>(entity =>
        {
            entity.HasIndex(c => c.UsuarioId).IsUnique();
            entity.Property(c => c.IngresosMensuales).HasPrecision(18, 2);
        });

        // SolicitudCredito
        builder.Entity<SolicitudCredito>(entity =>
        {
            entity.Property(s => s.MontoSolicitado).HasPrecision(18, 2);
            entity.HasOne(s => s.Cliente)
                  .WithMany(c => c.Solicitudes)
                  .HasForeignKey(s => s.ClienteId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Restricción: Un cliente solo puede tener una solicitud en estado Pendiente
            entity.HasIndex(s => s.ClienteId)
                  .HasFilter("Estado = 'Pendiente'")
                  .IsUnique();
        });

        // Notificacion
        builder.Entity<Notificacion>(entity =>
        {
            // Unicidad del MessageId para evitar duplicados por redelivery
            entity.HasIndex(n => n.MessageId).IsUnique();
            entity.HasIndex(n => n.UsuarioId);
        });
    }
}
