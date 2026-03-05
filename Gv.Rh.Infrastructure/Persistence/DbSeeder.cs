using Gv.Rh.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gv.Rh.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task SeedAsync(RhDbContext db)
    {
        await db.Database.MigrateAsync();

        await EnsureUserAsync(db,
            email: "admin@rh.local",
            password: "Admin123*",
            role: "ADMIN",
            mustChangePassword: false);

        await EnsureUserAsync(db,
            email: "rrhh@rh.local",
            password: "Rrhh123*",
            role: "RRHH",
            mustChangePassword: false);
    }

    private static async Task EnsureUserAsync(
        RhDbContext db,
        string email,
        string password,
        string role,
        bool mustChangePassword)
    {
        var exists = await db.Users.AnyAsync(x => x.Email == email);
        if (exists) return;

        db.Users.Add(new AppUser
        {
            Email = email,
            Role = role,
            IsActive = true,
            MustChangePassword = mustChangePassword,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
        });

        await db.SaveChangesAsync();
    }
}