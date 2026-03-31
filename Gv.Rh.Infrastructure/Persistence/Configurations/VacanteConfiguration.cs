using Gv.Rh.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gv.Rh.Infrastructure.Persistence.Configurations;

public sealed class VacanteConfiguration : IEntityTypeConfiguration<Vacante>
{
    public void Configure(EntityTypeBuilder<Vacante> builder)
    {
        builder.ToTable("reclutamiento_vacantes");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Folio)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Titulo)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.Descripcion)
            .HasMaxLength(4000);

        builder.Property(x => x.Perfil)
            .HasMaxLength(4000);

        builder.Property(x => x.SalarioMinimo)
            .HasPrecision(18, 2);

        builder.Property(x => x.SalarioMaximo)
            .HasPrecision(18, 2);

        builder.Property(x => x.FechaApertura)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(x => x.FechaCierre)
            .HasColumnType("date");

        builder.Property(x => x.Estatus)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Activo)
            .HasDefaultValue(true);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(x => x.Folio)
            .IsUnique();

        builder.HasIndex(x => x.Titulo);
        builder.HasIndex(x => x.Estatus);
        builder.HasIndex(x => x.DepartamentoId);
        builder.HasIndex(x => x.PuestoId);
        builder.HasIndex(x => x.SucursalId);

        builder.HasOne(x => x.Departamento)
            .WithMany()
            .HasForeignKey(x => x.DepartamentoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Puesto)
            .WithMany()
            .HasForeignKey(x => x.PuestoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Sucursal)
            .WithMany()
            .HasForeignKey(x => x.SucursalId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}