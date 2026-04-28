using Gv.Rh.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gv.Rh.Infrastructure.Persistence.Configurations;

public sealed class EmpleadoConfiguration : IEntityTypeConfiguration<Empleado>
{
    public void Configure(EntityTypeBuilder<Empleado> builder)
    {
        builder.ToTable("Empleados");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.NumEmpleado)
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(x => x.NumEmpleado)
            .IsUnique();

        builder.Property(x => x.Nombres)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(x => x.ApellidoPaterno)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(x => x.ApellidoMaterno)
            .HasMaxLength(120);

        builder.Property(x => x.Telefono)
            .HasMaxLength(15);

        builder.Property(x => x.Email)
            .HasMaxLength(160);

        builder.Property(x => x.Curp)
            .HasMaxLength(18);

        builder.Property(x => x.Rfc)
            .HasMaxLength(13);

        builder.Property(x => x.Nss)
            .HasMaxLength(11);

        builder.Property(x => x.Nacionalidad)
            .HasMaxLength(80);

        builder.Property(x => x.DireccionCalle)
            .HasMaxLength(150);

        builder.Property(x => x.DireccionNumeroExterior)
            .HasMaxLength(20);

        builder.Property(x => x.DireccionNumeroInterior)
            .HasMaxLength(20);

        builder.Property(x => x.DireccionColonia)
            .HasMaxLength(120);

        builder.Property(x => x.DireccionCiudad)
            .HasMaxLength(120);

        builder.Property(x => x.DireccionEstado)
            .HasMaxLength(120);

        builder.Property(x => x.DireccionCodigoPostal)
            .HasMaxLength(5);

        builder.Property(x => x.CodigoPostalFiscal)
            .HasMaxLength(5);

        builder.Property(x => x.EntidadFiscal)
            .HasMaxLength(120);

        builder.Property(x => x.ContactoEmergenciaNombre)
            .HasMaxLength(150);

        builder.Property(x => x.ContactoEmergenciaTelefono)
            .HasMaxLength(15);

        builder.Property(x => x.ContactoEmergenciaParentesco)
            .HasMaxLength(60);

        builder.Property(x => x.FotoNombreOriginal)
            .HasMaxLength(260);

        builder.Property(x => x.FotoNombreGuardado)
            .HasMaxLength(260);

        builder.Property(x => x.FotoRutaRelativa)
            .HasMaxLength(500);

        builder.Property(x => x.FotoMimeType)
            .HasMaxLength(100);

        builder.Property(x => x.Sexo)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.EstadoCivil)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.EstatusLaboralActual)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.TipoBajaActual)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.FotoUpdatedAtUtc)
            .HasColumnType("timestamp with time zone");

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

        builder.HasOne(x => x.AprobadorPrimario)
            .WithMany(x => x.SubordinadosComoAprobadorPrimario)
            .HasForeignKey(x => x.AprobadorPrimarioEmpleadoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AprobadorSecundario)
            .WithMany(x => x.SubordinadosComoAprobadorSecundario)
            .HasForeignKey(x => x.AprobadorSecundarioEmpleadoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.AprobadorPrimarioEmpleadoId);

        builder.HasIndex(x => x.AprobadorSecundarioEmpleadoId);
    }
}