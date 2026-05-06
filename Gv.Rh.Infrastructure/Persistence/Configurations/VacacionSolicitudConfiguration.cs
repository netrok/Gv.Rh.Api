using Gv.Rh.Domain.Common;
using Gv.Rh.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gv.Rh.Infrastructure.Persistence.Configurations;

public sealed class VacacionSolicitudConfiguration : IEntityTypeConfiguration<VacacionSolicitud>
{
    public void Configure(EntityTypeBuilder<VacacionSolicitud> builder)
    {
        builder.ToTable("vacacion_solicitudes");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.FechaInicio)
            .IsRequired();

        builder.Property(x => x.FechaFin)
            .IsRequired();

        builder.Property(x => x.DiasSolicitados)
            .HasPrecision(7, 2)
            .IsRequired();

        builder.Property(x => x.Estatus)
            .HasConversion(
                v => v.ToString(),
                v => string.IsNullOrWhiteSpace(v)
                    ? EstatusVacacionSolicitud.PENDIENTE
                    : Enum.Parse<EstatusVacacionSolicitud>(v, true))
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.ComentarioEmpleado)
            .HasMaxLength(1000);

        builder.Property(x => x.ComentarioResolucion)
            .HasMaxLength(1000);

        builder.Property(x => x.MotivoRechazo)
            .HasMaxLength(1000);

        builder.Property(x => x.FechaResolucionUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasOne(x => x.Empleado)
            .WithMany()
            .HasForeignKey(x => x.EmpleadoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.VacacionPeriodo)
            .WithMany()
            .HasForeignKey(x => x.VacacionPeriodoId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.VacacionMovimiento)
            .WithMany()
            .HasForeignKey(x => x.VacacionMovimientoId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Incidencia)
            .WithMany()
            .HasForeignKey(x => x.IncidenciaId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.SolicitadaPorUsuario)
            .WithMany()
            .HasForeignKey(x => x.SolicitadaPorUsuarioId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.ResueltaPorUsuario)
            .WithMany()
            .HasForeignKey(x => x.ResueltaPorUsuarioId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.AprobadorEmpleado)
            .WithMany()
            .HasForeignKey(x => x.AprobadorEmpleadoId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.EmpleadoId);
        builder.HasIndex(x => x.VacacionPeriodoId);
        builder.HasIndex(x => x.VacacionMovimientoId);
        builder.HasIndex(x => x.IncidenciaId);
        builder.HasIndex(x => x.SolicitadaPorUsuarioId);
        builder.HasIndex(x => x.ResueltaPorUsuarioId);
        builder.HasIndex(x => x.AprobadorEmpleadoId);
        builder.HasIndex(x => x.Estatus);
        builder.HasIndex(x => x.FechaInicio);
        builder.HasIndex(x => x.FechaFin);
        builder.HasIndex(x => x.CreatedAtUtc);
        builder.HasIndex(x => new { x.EmpleadoId, x.Estatus });
    }
}