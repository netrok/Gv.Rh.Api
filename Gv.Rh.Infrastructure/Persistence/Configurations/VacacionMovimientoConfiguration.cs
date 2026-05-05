using Gv.Rh.Domain.Common;
using Gv.Rh.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gv.Rh.Infrastructure.Persistence.Configurations;

public class VacacionMovimientoConfiguration : IEntityTypeConfiguration<VacacionMovimiento>
{
    public void Configure(EntityTypeBuilder<VacacionMovimiento> builder)
    {
        builder.ToTable("vacacion_movimientos");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CicloLaboral)
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(x => x.TipoMovimiento)
            .HasConversion(
                v => v.ToString(),
                v => string.IsNullOrWhiteSpace(v)
                    ? TipoMovimientoVacacion.AJUSTE_POSITIVO
                    : Enum.Parse<TipoMovimientoVacacion>(v, true))
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(x => x.FechaMovimiento)
            .IsRequired();

        builder.Property(x => x.FechaInicioDisfrute);

        builder.Property(x => x.FechaFinDisfrute);

        builder.Property(x => x.Dias)
            .HasPrecision(7, 2)
            .IsRequired();

        builder.Property(x => x.SaldoAntes)
            .HasPrecision(7, 2)
            .IsRequired();

        builder.Property(x => x.SaldoDespues)
            .HasPrecision(7, 2)
            .IsRequired();

        builder.Property(x => x.Referencia)
            .HasMaxLength(180);

        builder.Property(x => x.Comentario)
            .HasMaxLength(8000);

        builder.Property(x => x.Origen)
            .HasMaxLength(80);

        builder.Property(x => x.ImportacionArchivo)
            .HasMaxLength(260);

        builder.Property(x => x.ImportacionHoja)
            .HasMaxLength(160);

        builder.Property(x => x.ImportacionFila);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.HasOne(x => x.Empleado)
            .WithMany()
            .HasForeignKey(x => x.EmpleadoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.VacacionPeriodo)
            .WithMany(x => x.Movimientos)
            .HasForeignKey(x => x.VacacionPeriodoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.UsuarioResponsable)
            .WithMany()
            .HasForeignKey(x => x.UsuarioResponsableId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.EmpleadoId);
        builder.HasIndex(x => x.VacacionPeriodoId);
        builder.HasIndex(x => x.UsuarioResponsableId);
        builder.HasIndex(x => x.CicloLaboral);
        builder.HasIndex(x => x.TipoMovimiento);
        builder.HasIndex(x => x.FechaMovimiento);
        builder.HasIndex(x => x.Origen);

        builder.HasIndex(x => new { x.EmpleadoId, x.CicloLaboral, x.FechaMovimiento });
        builder.HasIndex(x => new { x.VacacionPeriodoId, x.FechaMovimiento });
    }
}
