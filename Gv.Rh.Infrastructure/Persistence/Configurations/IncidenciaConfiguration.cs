using Gv.Rh.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gv.Rh.Infrastructure.Persistence.Configurations;

public class IncidenciaConfiguration : IEntityTypeConfiguration<Incidencia>
{
    public void Configure(EntityTypeBuilder<Incidencia> b)
    {
        b.ToTable("incidencias");

        b.HasKey(x => x.Id);

        b.Property(x => x.Tipo)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        b.Property(x => x.Estatus)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        b.Property(x => x.Comentario)
            .HasMaxLength(1000);

        b.Property(x => x.ComentarioResolucion)
            .HasMaxLength(1000);

        b.Property(x => x.FechaInicio)
            .IsRequired();

        b.Property(x => x.FechaFin)
            .IsRequired();

        b.Property(x => x.FechaResolucionUtc)
            .HasColumnType("timestamp with time zone");

        b.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        b.Property(x => x.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        b.Property(x => x.EvidenciaNombreOriginal)
            .HasMaxLength(260);

        b.Property(x => x.EvidenciaNombreArchivo)
            .HasMaxLength(260);

        b.Property(x => x.EvidenciaContentType)
            .HasMaxLength(120);

        b.Property(x => x.EvidenciaRuta)
            .HasMaxLength(500);

        b.HasIndex(x => x.EmpleadoId);
        b.HasIndex(x => x.SucursalId);
        b.HasIndex(x => x.Tipo);
        b.HasIndex(x => x.Estatus);
        b.HasIndex(x => x.FechaInicio);
        b.HasIndex(x => x.FechaFin);
        b.HasIndex(x => x.ResueltaPorUsuarioId);
        b.HasIndex(x => x.ResueltaPorEmpleadoId);
        b.HasIndex(x => x.FechaResolucionUtc);

        b.HasOne(x => x.Empleado)
            .WithMany()
            .HasForeignKey(x => x.EmpleadoId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Sucursal)
            .WithMany()
            .HasForeignKey(x => x.SucursalId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.ResueltaPorUsuario)
            .WithMany()
            .HasForeignKey(x => x.ResueltaPorUsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.ResueltaPorEmpleado)
            .WithMany()
            .HasForeignKey(x => x.ResueltaPorEmpleadoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}