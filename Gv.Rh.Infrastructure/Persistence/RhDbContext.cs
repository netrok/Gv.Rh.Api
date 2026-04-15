using Gv.Rh.Domain.Common;
using Gv.Rh.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gv.Rh.Infrastructure.Persistence;

public class RhDbContext : DbContext
{
    public RhDbContext(DbContextOptions<RhDbContext> options) : base(options)
    {
    }

    public DbSet<Empleado> Empleados => Set<Empleado>();
    public DbSet<EmpleadoMovimientoLaboral> EmpleadoMovimientosLaborales => Set<EmpleadoMovimientoLaboral>();
    public DbSet<EmpleadoDocumento> EmpleadoDocumentos => Set<EmpleadoDocumento>();
    public DbSet<Departamento> Departamentos => Set<Departamento>();
    public DbSet<Puesto> Puestos => Set<Puesto>();
    public DbSet<Sucursal> Sucursales => Set<Sucursal>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Incidencia> Incidencias => Set<Incidencia>();

    // ===== Reclutamiento =====
    public DbSet<Vacante> Vacantes => Set<Vacante>();
    public DbSet<Candidato> Candidatos => Set<Candidato>();
    public DbSet<Postulacion> Postulaciones => Set<Postulacion>();
    public DbSet<PostulacionSeguimiento> PostulacionesSeguimientos => Set<PostulacionSeguimiento>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Secuencia para folios de vacantes
        modelBuilder.HasSequence<int>("vacantes_folio_seq")
            .StartsAt(1)
            .IncrementsBy(1);

        // Aplica configuraciones externas tipo IEntityTypeConfiguration<T>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RhDbContext).Assembly);

        // ===== Departamentos =====
        modelBuilder.Entity<Departamento>(d =>
        {
            d.ToTable("departamentos");
            d.HasKey(x => x.Id);

            d.Property(x => x.Clave)
                .HasMaxLength(20)
                .IsRequired();

            d.Property(x => x.Nombre)
                .HasMaxLength(150)
                .IsRequired();

            d.Property(x => x.Activo)
                .IsRequired();

            d.Property(x => x.CreatedAtUtc)
                .IsRequired();

            d.Property(x => x.UpdatedAtUtc)
                .IsRequired();

            d.HasIndex(x => x.Clave).IsUnique();
            d.HasIndex(x => x.Nombre);
        });

        // ===== Puestos =====
        modelBuilder.Entity<Puesto>(p =>
        {
            p.ToTable("puestos");
            p.HasKey(x => x.Id);

            p.Property(x => x.Clave)
                .HasMaxLength(20)
                .IsRequired();

            p.Property(x => x.Nombre)
                .HasMaxLength(150)
                .IsRequired();

            p.Property(x => x.Activo)
                .IsRequired();

            p.Property(x => x.CreatedAtUtc)
                .IsRequired();

            p.Property(x => x.UpdatedAtUtc)
                .IsRequired();

            p.HasOne(x => x.Departamento)
                .WithMany()
                .HasForeignKey(x => x.DepartamentoId)
                .OnDelete(DeleteBehavior.Restrict);

            p.HasIndex(x => x.Clave).IsUnique();
            p.HasIndex(x => x.DepartamentoId);
            p.HasIndex(x => new { x.DepartamentoId, x.Nombre });
        });

        // ===== Sucursales =====
        modelBuilder.Entity<Sucursal>(s =>
        {
            s.ToTable("sucursales");
            s.HasKey(x => x.Id);

            s.Property(x => x.Clave)
                .HasMaxLength(20)
                .IsRequired();

            s.Property(x => x.Nombre)
                .HasMaxLength(150)
                .IsRequired();

            s.Property(x => x.Direccion)
                .HasMaxLength(250);

            s.Property(x => x.Telefono)
                .HasMaxLength(30);

            s.Property(x => x.Activo)
                .IsRequired();

            s.HasIndex(x => x.Clave).IsUnique();
            s.HasIndex(x => x.Nombre);
        });

        // ===== Empleados =====
        modelBuilder.Entity<Empleado>(e =>
        {
            e.ToTable("empleados");
            e.HasKey(x => x.Id);

            e.Property(x => x.NumEmpleado)
                .HasMaxLength(20)
                .IsRequired();

            e.Property(x => x.Nombres)
                .HasMaxLength(120)
                .IsRequired();

            e.Property(x => x.ApellidoPaterno)
                .HasMaxLength(120)
                .IsRequired();

            e.Property(x => x.ApellidoMaterno)
                .HasMaxLength(120);

            e.Property(x => x.Telefono)
                .HasMaxLength(15);

            e.Property(x => x.Email)
                .HasMaxLength(160);

            e.Property(x => x.FechaIngreso)
                .IsRequired();

            e.Property(x => x.Activo)
                .IsRequired();

            e.Property(x => x.EstatusLaboralActual)
                .HasConversion(
                    v => v.ToString(),
                    v => string.IsNullOrWhiteSpace(v)
                        ? EstatusLaboralEmpleado.ACTIVO
                        : Enum.Parse<EstatusLaboralEmpleado>(v, true))
                .HasMaxLength(30)
                .IsRequired();

            e.Property(x => x.TipoBajaActual)
                .HasConversion(
                    v => v.HasValue ? v.Value.ToString() : null,
                    v => string.IsNullOrWhiteSpace(v)
                        ? (TipoBajaEmpleado?)null
                        : Enum.Parse<TipoBajaEmpleado>(v, true))
                .HasMaxLength(50);

            e.Property(x => x.FechaBajaActual);

            e.Property(x => x.FechaReingresoActual);

            e.Property(x => x.Recontratable);

            // Identificación
            e.Property(x => x.Curp)
                .HasMaxLength(18);

            e.Property(x => x.Rfc)
                .HasMaxLength(13);

            e.Property(x => x.Nss)
                .HasMaxLength(11);

            // Personales
            e.Property(x => x.Sexo)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            e.Property(x => x.EstadoCivil)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            e.Property(x => x.Nacionalidad)
                .HasMaxLength(80);

            // Domicilio
            e.Property(x => x.DireccionCalle)
                .HasMaxLength(150);

            e.Property(x => x.DireccionNumeroExterior)
                .HasMaxLength(20);

            e.Property(x => x.DireccionNumeroInterior)
                .HasMaxLength(20);

            e.Property(x => x.DireccionColonia)
                .HasMaxLength(120);

            e.Property(x => x.DireccionCiudad)
                .HasMaxLength(120);

            e.Property(x => x.DireccionEstado)
                .HasMaxLength(120);

            e.Property(x => x.DireccionCodigoPostal)
                .HasMaxLength(5);

            // Emergencia
            e.Property(x => x.ContactoEmergenciaNombre)
                .HasMaxLength(150);

            e.Property(x => x.ContactoEmergenciaTelefono)
                .HasMaxLength(15);

            e.Property(x => x.ContactoEmergenciaParentesco)
                .HasMaxLength(60);

            // Foto
            e.Property(x => x.FotoRutaRelativa)
                .HasMaxLength(300);

            e.Property(x => x.FotoNombreOriginal)
                .HasMaxLength(255);

            e.Property(x => x.FotoMimeType)
                .HasMaxLength(100);

            e.Property(x => x.FotoTamanoBytes);

            e.Property(x => x.FotoUpdatedAtUtc);

            e.HasOne(x => x.Departamento)
                .WithMany()
                .HasForeignKey(x => x.DepartamentoId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Puesto)
                .WithMany()
                .HasForeignKey(x => x.PuestoId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Sucursal)
                .WithMany(x => x.Empleados)
                .HasForeignKey(x => x.SucursalId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(x => x.Documentos)
                .WithOne(x => x.Empleado)
                .HasForeignKey(x => x.EmpleadoId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(x => x.MovimientosLaborales)
                .WithOne(x => x.Empleado)
                .HasForeignKey(x => x.EmpleadoId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => x.NumEmpleado).IsUnique();
            e.HasIndex(x => x.Email);
            e.HasIndex(x => x.Curp);
            e.HasIndex(x => x.Rfc);
            e.HasIndex(x => x.Nss);
            e.HasIndex(x => x.DepartamentoId);
            e.HasIndex(x => x.PuestoId);
            e.HasIndex(x => x.SucursalId);
            e.HasIndex(x => x.Activo);
            e.HasIndex(x => x.EstatusLaboralActual);
        });

        // ===== Users =====
        modelBuilder.Entity<AppUser>(u =>
        {
            u.ToTable("users");
            u.HasKey(x => x.Id);

            u.Property(x => x.Email)
                .HasMaxLength(160)
                .IsRequired();

            u.Property(x => x.PasswordHash)
                .HasMaxLength(300)
                .IsRequired();

            u.Property(x => x.FullName)
                .HasMaxLength(160)
                .IsRequired();

            u.Property(x => x.Role)
                .HasMaxLength(50)
                .IsRequired()
                .HasDefaultValue(UserRoles.Consulta);

            u.Property(x => x.IsActive)
                .IsRequired();

            u.Property(x => x.MustChangePassword)
                .IsRequired();

            u.Property(x => x.CreatedAtUtc)
                .IsRequired();

            u.Property(x => x.UpdatedAtUtc);

            u.HasIndex(x => x.Email).IsUnique();

            u.HasOne(x => x.Empleado)
                .WithOne()
                .HasForeignKey<AppUser>(x => x.EmpleadoId)
                .OnDelete(DeleteBehavior.SetNull);

            u.HasIndex(x => x.EmpleadoId).IsUnique();
        });

        // ===== Refresh Tokens =====
        modelBuilder.Entity<RefreshToken>(t =>
        {
            t.ToTable("auth_refresh_tokens");
            t.HasKey(x => x.Id);

            t.Property(x => x.TokenHash)
                .HasMaxLength(80)
                .IsRequired();

            t.Property(x => x.ReplacedByTokenHash)
                .HasMaxLength(80);

            t.Property(x => x.RevokedReason)
                .HasMaxLength(40);

            t.Property(x => x.CreatedAtUtc)
                .IsRequired();

            t.Property(x => x.ExpiresAtUtc)
                .IsRequired();

            t.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            t.HasIndex(x => x.UserId);
            t.HasIndex(x => x.TokenHash).IsUnique();
            t.HasIndex(x => x.ExpiresAtUtc);
        });

        // ===== Audit Logs =====
        modelBuilder.Entity<AuditLog>(a =>
        {
            a.ToTable("audit_logs");
            a.HasKey(x => x.Id);

            a.Property(x => x.OccurredAtUtc)
                .IsRequired();

            a.Property(x => x.Action)
                .HasMaxLength(50)
                .IsRequired();

            a.Property(x => x.EntityName)
                .HasMaxLength(100)
                .IsRequired();

            a.Property(x => x.RecordId)
                .HasMaxLength(100)
                .IsRequired();

            a.Property(x => x.UserEmail)
                .HasMaxLength(200);

            a.Property(x => x.UserRole)
                .HasMaxLength(100);

            a.Property(x => x.IpAddress)
                .HasMaxLength(100);

            a.Property(x => x.UserAgent)
                .HasMaxLength(1000);

            a.Property(x => x.OldValuesJson)
                .HasColumnType("jsonb");

            a.Property(x => x.NewValuesJson)
                .HasColumnType("jsonb");

            a.Property(x => x.ChangedColumnsJson)
                .HasColumnType("jsonb");

            a.HasIndex(x => x.OccurredAtUtc);
            a.HasIndex(x => x.Action);
            a.HasIndex(x => x.EntityName);
            a.HasIndex(x => x.UserId);
            a.HasIndex(x => new { x.EntityName, x.RecordId });
        });
    }
}