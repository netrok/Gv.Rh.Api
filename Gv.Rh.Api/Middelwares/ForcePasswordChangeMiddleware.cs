using Gv.Rh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Gv.Rh.Api.Middlewares;

public class ForcePasswordChangeMiddleware
{
    private readonly RequestDelegate _next;

    public ForcePasswordChangeMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, RhDbContext db)
    {
        var path = (context.Request.Path.Value ?? "").ToLowerInvariant();

        // Públicos / estáticos / Swagger
        if (path.StartsWith("/swagger") ||
            path == "/" ||
            path.StartsWith("/favicon") ||
            path.StartsWith("/index.html"))
        {
            await _next(context);
            return;
        }

        // Auth endpoints que SIEMPRE deben pasar (incluye login)
        if (path.StartsWith("/api/auth/login") ||
            path.StartsWith("/api/auth/refresh")) // si tú decides bloquear refresh cuando mustChange, lo quitamos abajo
        {
            await _next(context);
            return;
        }

        // Si no está autenticado, no aplica
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        // Extraer userId
        var sub = context.User.FindFirstValue("sub") ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(sub, out var userId))
        {
            await _next(context);
            return;
        }

        // Consultar MustChangePassword + existencia de usuario
        var userState = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Id, u.IsActive, u.MustChangePassword })
            .FirstOrDefaultAsync();

        if (userState is null || !userState.IsActive)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsync("{\"message\":\"Sesión inválida.\",\"code\":\"INVALID_SESSION\"}");
            return;
        }

        if (!userState.MustChangePassword)
        {
            await _next(context);
            return;
        }

        // Si MustChangePassword=true, solo dejamos pasar lo mínimo indispensable
        var allowed =
            path == "/api/auth/change-password" ||
            path == "/api/auth/whoami" ||
            path == "/api/auth/logout" ||
            path == "/api/auth/logout-all";

        // ⚠️ Recomendación: NO permitir refresh aquí (cierra hueco).
        // Si por alguna razón quieres permitirlo, agrega:
        // || path == "/api/auth/refresh"

        if (allowed)
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsync(
            "{\"message\":\"Debes cambiar tu contraseña antes de usar el sistema.\",\"code\":\"MUST_CHANGE_PASSWORD\"}"
        );
    }
}