using Gv.Rh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gv.Rh.Api.Controllers;

[ApiController]
[Route("api/admin/links")]
[Authorize(Roles = "ADMIN")]
public class AdminLinksController : ControllerBase
{
    private readonly RhDbContext _db;

    public AdminLinksController(RhDbContext db) => _db = db;

    // GET /api/admin/links/pending
    [HttpGet("pending")]
    public async Task<IActionResult> Pending()
    {
        // 1) Empleados con email y SIN usuario ligado (por EmpleadoId)
        var empleadosSinCuenta = await _db.Empleados.AsNoTracking()
            .Where(e => e.Email != null && e.Email != "")
            .Where(e => !_db.Users.Any(u => u.EmpleadoId == e.Id))
            .Select(e => new
            {
                empleadoId = e.Id,
                e.NumEmpleado,
                e.Nombres,
                e.ApellidoPaterno,
                e.ApellidoMaterno,
                email = e.Email!.Trim().ToLower(),
                // ¿existe usuario con ese email (aunque no esté ligado)?
                hasUserSameEmail = _db.Users.Any(u => u.Email.ToLower() == e.Email!.Trim().ToLower())
            })
            .OrderBy(e => e.empleadoId)
            .ToListAsync();

        // 2) Usuarios sin empleado ligado, pero con empleado por email y ese empleado no tiene cuenta
        var usuariosSinEmpleado = await _db.Users.AsNoTracking()
            .Where(u => u.EmpleadoId == null)
            .Join(
                _db.Empleados.AsNoTracking().Where(e => e.Email != null && e.Email != ""),
                u => u.Email.ToLower(),
                e => e.Email!.Trim().ToLower(),
                (u, e) => new { u, e }
            )
            .Where(x => !_db.Users.Any(u2 => u2.EmpleadoId == x.e.Id)) // empleado aún sin cuenta
            .Select(x => new
            {
                userId = x.u.Id,
                email = x.u.Email,
                x.u.Role,
                x.u.IsActive,
                x.u.MustChangePassword,
                empleado = new
                {
                    empleadoId = x.e.Id,
                    x.e.NumEmpleado,
                    x.e.Nombres,
                    x.e.ApellidoPaterno,
                    x.e.ApellidoMaterno,
                    email = x.e.Email
                }
            })
            .OrderBy(x => x.userId)
            .ToListAsync();

        return Ok(new
        {
            empleadosSinCuenta,
            usuariosSinEmpleado
        });
    }

    public record BulkLinkResult(
        int linked,
        int skipped,
        List<object> details
    );

    // POST /api/admin/links/link-all-by-email?dryRun=true
    [HttpPost("link-all-by-email")]
    public async Task<IActionResult> LinkAllByEmail([FromQuery] bool dryRun = true)
    {
        // Candidatos: usuarios sin empleadoId
        var users = await _db.Users
            .Where(u => u.EmpleadoId == null)
            .ToListAsync();

        // Empleados con email
        var empleados = await _db.Empleados
            .Where(e => e.Email != null && e.Email != "")
            .Select(e => new { e.Id, Email = e.Email!.Trim().ToLower() })
            .ToListAsync();

        // email -> empleadoId (solo si es único, evita ambigüedad)
        var empByEmail = empleados
            .GroupBy(x => x.Email)
            .Where(g => g.Count() == 1)
            .ToDictionary(g => g.Key, g => g.First().Id);

        // empleados que YA tienen user ligado
        var empleadosConCuenta = await _db.Users
            .Where(u => u.EmpleadoId != null)
            .Select(u => u.EmpleadoId!.Value)
            .Distinct()
            .ToListAsync();

        var empleadosConCuentaSet = empleadosConCuenta.ToHashSet();

        var details = new List<object>();
        var linked = 0;
        var skipped = 0;

        foreach (var u in users)
        {
            var email = (u.Email ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email))
            {
                skipped++;
                details.Add(new { userId = u.Id, email = u.Email, reason = "USER_EMAIL_EMPTY" });
                continue;
            }

            if (!empByEmail.TryGetValue(email, out var empId))
            {
                skipped++;
                details.Add(new { userId = u.Id, email = u.Email, reason = "NO_EMP_MATCH_OR_AMBIGUOUS" });
                continue;
            }

            if (empleadosConCuentaSet.Contains(empId))
            {
                skipped++;
                details.Add(new { userId = u.Id, email = u.Email, empleadoId = empId, reason = "EMP_ALREADY_LINKED" });
                continue;
            }

            // OK -> ligar
            u.EmpleadoId = empId;
            empleadosConCuentaSet.Add(empId);
            linked++;

            details.Add(new { userId = u.Id, email = u.Email, empleadoId = empId, action = "LINKED" });
        }

        if (!dryRun)
            await _db.SaveChangesAsync();

        return Ok(new BulkLinkResult(linked, skipped, details));
    }
}