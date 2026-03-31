using Gv.Rh.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gv.Rh.Infrastructure.Persistence.Configurations;

public sealed class CandidatoConfiguration : IEntityTypeConfiguration<Candidato>
{
    public void Configure(EntityTypeBuilder<Candidato> builder)
    {
        builder.ToTable("reclutamiento_candidatos");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Nombres)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.ApellidoPaterno)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.ApellidoMaterno)
            .HasMaxLength(150);

        builder.Property(x => x.Telefono)
            .HasMaxLength(30);

        builder.Property(x => x.Email)
            .HasMaxLength(200);

        builder.Property(x => x.FechaNacimiento)
            .HasColumnType("date");

        builder.Property(x => x.Ciudad)
            .HasMaxLength(150);

        builder.Property(x => x.FuenteReclutamiento)
            .HasMaxLength(100);

        builder.Property(x => x.ResumenPerfil)
            .HasMaxLength(4000);

        builder.Property(x => x.PretensionSalarial)
            .HasPrecision(18, 2);

        builder.Property(x => x.CvNombreOriginal)
            .HasMaxLength(260);

        builder.Property(x => x.CvNombreGuardado)
            .HasMaxLength(260);

        builder.Property(x => x.CvRutaRelativa)
            .HasMaxLength(500);

        builder.Property(x => x.CvMimeType)
            .HasMaxLength(150);

        builder.Property(x => x.Activo)
            .HasDefaultValue(true);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(x => x.Email);
        builder.HasIndex(x => x.Telefono);
        builder.HasIndex(x => x.FuenteReclutamiento);
        builder.HasIndex(x => x.Activo);
    }
}