using Gv.Rh.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gv.Rh.Infrastructure.Persistence.Configurations;

public class EmpleadoDocumentoConfiguration : IEntityTypeConfiguration<EmpleadoDocumento>
{
    public void Configure(EntityTypeBuilder<EmpleadoDocumento> builder)
    {
        builder.ToTable("empleado_documentos");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Tipo)
            .IsRequired();

        builder.Property(x => x.NombreArchivoOriginal)
            .HasMaxLength(260)
            .IsRequired();

        builder.Property(x => x.NombreArchivoGuardado)
            .HasMaxLength(260)
            .IsRequired();

        builder.Property(x => x.RutaRelativa)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.MimeType)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.TamanoBytes)
            .IsRequired();

        builder.Property(x => x.Comentario)
            .HasMaxLength(1000);

        builder.Property(x => x.Activo)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        builder.HasOne(x => x.Empleado)
            .WithMany()
            .HasForeignKey(x => x.EmpleadoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.EmpleadoId);
        builder.HasIndex(x => x.Tipo);
        builder.HasIndex(x => x.Activo);
        builder.HasIndex(x => x.FechaVencimiento);
        builder.HasIndex(x => new { x.EmpleadoId, x.Activo });
    }
}