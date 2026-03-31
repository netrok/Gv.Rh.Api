using Gv.Rh.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gv.Rh.Infrastructure.Persistence.Configurations;

public sealed class PostulacionSeguimientoConfiguration : IEntityTypeConfiguration<PostulacionSeguimiento>
{
    public void Configure(EntityTypeBuilder<PostulacionSeguimiento> builder)
    {
        builder.ToTable("reclutamiento_postulaciones_seguimiento");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.EtapaAnterior)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.EtapaNueva)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Comentario)
            .HasMaxLength(2000);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(x => x.PostulacionId);
        builder.HasIndex(x => x.CreatedAtUtc);

        builder.HasOne(x => x.Postulacion)
            .WithMany(x => x.Seguimientos)
            .HasForeignKey(x => x.PostulacionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.CreadoPorUser)
            .WithMany()
            .HasForeignKey(x => x.CreadoPorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}