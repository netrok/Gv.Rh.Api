using Gv.Rh.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gv.Rh.Infrastructure.Persistence.Configurations;

public sealed class PostulacionConfiguration : IEntityTypeConfiguration<Postulacion>
{
    public void Configure(EntityTypeBuilder<Postulacion> builder)
    {
        builder.ToTable("reclutamiento_postulaciones");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.EtapaActual)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.ObservacionesInternas)
            .HasMaxLength(2000);

        builder.Property(x => x.MotivoDescarte)
            .HasMaxLength(1000);

        builder.Property(x => x.Activo)
            .HasDefaultValue(true);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.FechaPostulacionUtc)
            .IsRequired();

        builder.Property(x => x.FechaUltimoMovimientoUtc)
            .IsRequired();

        builder.HasIndex(x => x.VacanteId);
        builder.HasIndex(x => x.CandidatoId);
        builder.HasIndex(x => x.EtapaActual);
        builder.HasIndex(x => x.EsContratado);
        builder.HasIndex(x => new { x.VacanteId, x.CandidatoId });

        builder.HasOne(x => x.Vacante)
            .WithMany(x => x.Postulaciones)
            .HasForeignKey(x => x.VacanteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Candidato)
            .WithMany(x => x.Postulaciones)
            .HasForeignKey(x => x.CandidatoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Empleado)
            .WithMany()
            .HasForeignKey(x => x.EmpleadoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}