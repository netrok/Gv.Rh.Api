using Gv.Rh.Domain.Common;
using Gv.Rh.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gv.Rh.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task SeedAsync(RhDbContext db)
    {
        async Task UpsertUserAsync(
            string email,
            string fullName,
            string password,
            string role,
            bool isActive,
            bool mustChangePassword = true)
        {
            email = email.Trim().ToLowerInvariant();
            fullName = fullName.Trim();
            role = UserRoles.Normalize(role);

            var existing = await db.Users.FirstOrDefaultAsync(x => x.Email == email);
            if (existing is not null)
                return;

            db.Users.Add(new AppUser
            {
                Email = email,
                FullName = fullName,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = role,
                IsActive = isActive,
                MustChangePassword = mustChangePassword,
                CreatedAtUtc = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        }

        async Task SeedVacacionesPoliticaAsync()
        {
            const string politicaNombre = "Vacaciones dignas 2023";

            var detallesSeed = new List<VacacionPoliticaDetalleSeed>
            {
                new(1, 1, 12m),
                new(2, 2, 14m),
                new(3, 3, 16m),
                new(4, 4, 18m),
                new(5, 5, 20m),
                new(6, 10, 22m),
                new(11, 15, 24m),
                new(16, 20, 26m),
                new(21, 25, 28m),
                new(26, 30, 30m),
                new(31, 35, 32m),
                new(36, 40, 34m),
                new(41, 45, 36m),
                new(46, 50, 38m)
            };

            var politica = await db.VacacionPoliticas
                .Include(x => x.Detalles)
                .FirstOrDefaultAsync(x => x.Nombre == politicaNombre);

            if (politica is null)
            {
                politica = new VacacionPolitica
                {
                    Nombre = politicaNombre,
                    VigenteDesde = new DateOnly(2023, 1, 1),
                    VigenteHasta = null,
                    PrimaVacacionalPorcentaje = 25m,
                    Activo = true,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow,
                    Detalles = detallesSeed
                        .Select(x => new VacacionPoliticaDetalle
                        {
                            AnioServicioDesde = x.AnioServicioDesde,
                            AnioServicioHasta = x.AnioServicioHasta,
                            DiasVacaciones = x.DiasVacaciones,
                            CreatedAtUtc = DateTime.UtcNow
                        })
                        .ToList()
                };

                db.VacacionPoliticas.Add(politica);
                await db.SaveChangesAsync();
                return;
            }

            politica.VigenteDesde = new DateOnly(2023, 1, 1);
            politica.VigenteHasta = null;
            politica.PrimaVacacionalPorcentaje = 25m;
            politica.Activo = true;
            politica.UpdatedAtUtc = DateTime.UtcNow;

            foreach (var detalleSeed in detallesSeed)
            {
                var detalle = politica.Detalles.FirstOrDefault(x =>
                    x.AnioServicioDesde == detalleSeed.AnioServicioDesde &&
                    x.AnioServicioHasta == detalleSeed.AnioServicioHasta);

                if (detalle is null)
                {
                    politica.Detalles.Add(new VacacionPoliticaDetalle
                    {
                        VacacionPoliticaId = politica.Id,
                        AnioServicioDesde = detalleSeed.AnioServicioDesde,
                        AnioServicioHasta = detalleSeed.AnioServicioHasta,
                        DiasVacaciones = detalleSeed.DiasVacaciones,
                        CreatedAtUtc = DateTime.UtcNow
                    });

                    continue;
                }

                detalle.DiasVacaciones = detalleSeed.DiasVacaciones;
            }

            await db.SaveChangesAsync();
        }

        await UpsertUserAsync(
            email: "admin@rh.local",
            fullName: "Administrador",
            password: "Admin123*",
            role: UserRoles.Admin,
            isActive: true);

        await UpsertUserAsync(
            email: "rrhh@rh.local",
            fullName: "Recursos Humanos",
            password: "Rrhh123*",
            role: UserRoles.Rrhh,
            isActive: true);

        await SeedVacacionesPoliticaAsync();
    }

    private sealed record VacacionPoliticaDetalleSeed(
        int AnioServicioDesde,
        int? AnioServicioHasta,
        decimal DiasVacaciones);
}