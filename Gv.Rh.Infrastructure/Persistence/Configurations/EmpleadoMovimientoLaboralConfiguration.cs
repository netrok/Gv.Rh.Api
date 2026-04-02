using Gv.Rh.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gv.Rh.Infrastructure.Persistence.Configurations;

public class EmpleadoMovimientoLaboralConfiguration : IEntityTypeConfiguration<EmpleadoMovimientoLaboral>
{
    public void Configure(EntityTypeBuilder<EmpleadoMovimientoLaboral> builder)
    {
        builder.ToTable("empleado_movimientos_laborales");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TipoMovimiento)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.FechaMovimiento)
            .IsRequired();

        builder.Property(x => x.TipoBaja)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(x => x.Motivo)
            .HasMaxLength(200);

        builder.Property(x => x.Comentario)
            .HasMaxLength(1000);

        builder.Property(x => x.Recontratable);

        builder.Property(x => x.UsuarioResponsableId);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(x => x.EmpleadoId);

        builder.HasIndex(x => x.TipoMovimiento);

        builder.HasIndex(x => x.FechaMovimiento);

        builder.HasOne(x => x.Empleado)
            .WithMany(x => x.MovimientosLaborales)
            .HasForeignKey(x => x.EmpleadoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}