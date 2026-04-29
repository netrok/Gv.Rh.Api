using Gv.Rh.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gv.Rh.Infrastructure.Persistence.Configurations;

public class VacacionPoliticaConfiguration : IEntityTypeConfiguration<VacacionPolitica>
{
    public void Configure(EntityTypeBuilder<VacacionPolitica> builder)
    {
        builder.ToTable("vacacion_politicas");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Nombre)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.VigenteDesde)
            .IsRequired();

        builder.Property(x => x.VigenteHasta);

        builder.Property(x => x.PrimaVacacionalPorcentaje)
            .HasPrecision(7, 2)
            .IsRequired();

        builder.Property(x => x.Activo)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(x => x.Nombre);
        builder.HasIndex(x => x.Activo);
        builder.HasIndex(x => new { x.VigenteDesde, x.VigenteHasta });
    }
}
