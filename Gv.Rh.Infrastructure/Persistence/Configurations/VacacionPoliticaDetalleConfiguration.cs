using Gv.Rh.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gv.Rh.Infrastructure.Persistence.Configurations;

public class VacacionPoliticaDetalleConfiguration : IEntityTypeConfiguration<VacacionPoliticaDetalle>
{
    public void Configure(EntityTypeBuilder<VacacionPoliticaDetalle> builder)
    {
        builder.ToTable("vacacion_politica_detalles");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.AnioServicioDesde)
            .IsRequired();

        builder.Property(x => x.AnioServicioHasta);

        builder.Property(x => x.DiasVacaciones)
            .HasPrecision(7, 2)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.HasOne(x => x.VacacionPolitica)
            .WithMany(x => x.Detalles)
            .HasForeignKey(x => x.VacacionPoliticaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.VacacionPoliticaId);
        builder.HasIndex(x => new { x.VacacionPoliticaId, x.AnioServicioDesde, x.AnioServicioHasta })
            .IsUnique();
    }
}
