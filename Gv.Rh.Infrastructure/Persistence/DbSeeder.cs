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
    }
}