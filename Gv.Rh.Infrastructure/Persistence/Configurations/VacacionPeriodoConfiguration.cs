using Gv.Rh.Domain.Common;
using Gv.Rh.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gv.Rh.Infrastructure.Persistence.Configurations;

public class VacacionPeriodoConfiguration : IEntityTypeConfiguration<VacacionPeriodo>
{
    public void Configure(EntityTypeBuilder<VacacionPeriodo> builder)
    {
        builder.ToTable("vacacion_periodos");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CicloLaboral)
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(x => x.AnioServicio)
            .IsRequired();

        builder.Property(x => x.FechaInicio)
            .IsRequired();

        builder.Property(x => x.FechaFin)
            .IsRequired();

        builder.Property(x => x.FechaLimiteDisfrute)
            .IsRequired();

        builder.Property(x => x.DiasDerecho)
            .HasPrecision(7, 2)
            .IsRequired();

        builder.Property(x => x.DiasTomados)
            .HasPrecision(7, 2)
            .IsRequired();

        builder.Property(x => x.DiasPagados)
            .HasPrecision(7, 2)
            .IsRequired();

        builder.Property(x => x.DiasAjustados)
            .HasPrecision(7, 2)
            .IsRequired();

        builder.Property(x => x.DiasVencidos)
            .HasPrecision(7, 2)
            .IsRequired();

        builder.Property(x => x.Saldo)
            .HasPrecision(7, 2)
            .IsRequired();

        builder.Property(x => x.PrimaPagada)
            .IsRequired();

        builder.Property(x => x.FechaPagoPrima);

        builder.Property(x => x.Comentario)
            .HasMaxLength(1000);

        builder.Property(x => x.Estatus)
            .HasConversion(
                v => v.ToString(),
                v => string.IsNullOrWhiteSpace(v)
                    ? EstatusVacacionPeriodo.ABIERTO
                    : Enum.Parse<EstatusVacacionPeriodo>(v, true))
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        builder.HasOne(x => x.Empleado)
            .WithMany()
            .HasForeignKey(x => x.EmpleadoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.VacacionPolitica)
            .WithMany(x => x.Periodos)
            .HasForeignKey(x => x.VacacionPoliticaId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Movimientos)
            .WithOne(x => x.VacacionPeriodo)
            .HasForeignKey(x => x.VacacionPeriodoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.EmpleadoId);
        builder.HasIndex(x => x.VacacionPoliticaId);
        builder.HasIndex(x => x.CicloLaboral);
        builder.HasIndex(x => x.AnioServicio);
        builder.HasIndex(x => x.Estatus);

        builder.HasIndex(x => new { x.EmpleadoId, x.CicloLaboral, x.AnioServicio })
            .IsUnique();

        builder.HasIndex(x => new { x.EmpleadoId, x.CicloLaboral, x.FechaInicio, x.FechaFin });
    }
}