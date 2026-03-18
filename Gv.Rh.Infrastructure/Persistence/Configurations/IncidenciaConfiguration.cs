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

        b.Property(x => x.FechaInicio)
            .IsRequired();

        b.Property(x => x.FechaFin)
            .IsRequired();

        b.Property(x => x.CreatedAtUtc)
            .IsRequired();

        b.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        b.HasIndex(x => x.EmpleadoId);
        b.HasIndex(x => x.SucursalId);
        b.HasIndex(x => x.Tipo);
        b.HasIndex(x => x.Estatus);
        b.HasIndex(x => x.FechaInicio);
        b.HasIndex(x => x.FechaFin);

        b.HasOne(x => x.Empleado)
            .WithMany()
            .HasForeignKey(x => x.EmpleadoId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Sucursal)
            .WithMany()
            .HasForeignKey(x => x.SucursalId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}