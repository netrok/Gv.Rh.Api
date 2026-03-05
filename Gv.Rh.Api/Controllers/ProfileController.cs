using Gv.Rh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Gv.Rh.Api.Controllers;

[ApiController]
[Route("api/profile")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly RhDbContext _db;

    public ProfileController(RhDbContext db) => _db = db;

    // GET /api/profile/me
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(sub, out var userId))
            return Unauthorized();

        var me = await _db.Users.AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new
            {
                x.Id,
                x.Email,
                x.Role,
                x.IsActive,
                x.MustChangePassword,
                x.EmpleadoId,
                x.CreatedAtUtc
            })
            .FirstOrDefaultAsync();

        return me is null ? Unauthorized() : Ok(me);
    }

    // GET /api/profile/me/empleado
    [HttpGet("me/empleado")]
    public async Task<IActionResult> MyEmpleado()
    {
        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(sub, out var userId))
            return Unauthorized();

        var empleadoId = await _db.Users.AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => x.EmpleadoId)
            .FirstOrDefaultAsync();

        if (!empleadoId.HasValue)
            return NotFound(new { message = "Este usuario no está ligado a un empleado." });

        var emp = await _db.Empleados.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == empleadoId.Value);

        return emp is null
            ? NotFound(new { message = "Empleado ligado no existe." })
            : Ok(emp);
    }
}