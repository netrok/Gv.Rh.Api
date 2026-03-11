using Gv.Rh.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gv.Rh.Infrastructure.Persistence;

public class RhDbContext : DbContext
{
    public RhDbContext(DbContextOptions<RhDbContext> options) : base(options) { }

    public DbSet<Empleado> Empleados => Set<Empleado>();
    public DbSet<Departamento> Departamentos => Set<Departamento>();
    public DbSet<Puesto> Puestos => Set<Puesto>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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
                .HasMaxLength(30);

            e.Property(x => x.Email)
                .HasMaxLength(160);

            e.HasOne(x => x.Departamento)
                .WithMany()
                .HasForeignKey(x => x.DepartamentoId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Puesto)
                .WithMany()
                .HasForeignKey(x => x.PuestoId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => x.NumEmpleado).IsUnique();
            e.HasIndex(x => x.DepartamentoId);
            e.HasIndex(x => x.PuestoId);
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

            u.Property(x => x.Role)
                .HasMaxLength(50)
                .IsRequired();

            u.Property(x => x.IsActive)
                .IsRequired();

            u.Property(x => x.MustChangePassword)
                .IsRequired();

            u.Property(x => x.CreatedAtUtc)
                .IsRequired();

            u.HasIndex(x => x.Email).IsUnique();

            // FK opcional a empleado (1 empleado = 0/1 usuario)
            u.HasOne(x => x.Empleado)
                .WithOne()
                .HasForeignKey<AppUser>(x => x.EmpleadoId)
                .OnDelete(DeleteBehavior.SetNull);

            // PostgreSQL permite múltiples NULL en índices únicos
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

        // ===== Audit Log =====
        modelBuilder.Entity<AuditLog>(a =>
        {
            a.ToTable("audit_log");
            a.HasKey(x => x.Id);

            a.Property(x => x.Action)
                .HasMaxLength(20)
                .IsRequired();

            a.Property(x => x.Entity)
                .HasMaxLength(80)
                .IsRequired();

            a.Property(x => x.EntityId)
                .HasMaxLength(64)
                .IsRequired();

            a.Property(x => x.Email)
                .HasMaxLength(160);

            a.Property(x => x.Role)
                .HasMaxLength(50);

            a.Property(x => x.Ip)
                .HasMaxLength(60);

            a.Property(x => x.UserAgent)
                .HasMaxLength(300);

            a.Property(x => x.ChangesJson)
                .HasColumnType("jsonb");

            a.HasIndex(x => x.OccurredAtUtc);
            a.HasIndex(x => x.Entity);
            a.HasIndex(x => x.EntityId);
        });
    }
}