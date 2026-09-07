using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
 
namespace Gv.Rh.Api.Middlewares;
 
/// <summary>
/// Manejador global de excepciones no capturadas. Traduce excepciones comunes
/// a respuestas HTTP consistentes (application/problem+json), evitando repetir
/// el mismo try/catch en cada accion de cada controller.
///
/// - KeyNotFoundException      -> 404 Not Found
/// - InvalidOperationException -> 400 Bad Request
/// - Cualquier otra excepcion  -> 500 Internal Server Error
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
 
    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }
 
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title) = exception switch
        {
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Recurso no encontrado."),
            InvalidOperationException => (StatusCodes.Status400BadRequest, "Solicitud invalida."),
            _ => (StatusCodes.Status500InternalServerError, "No fue posible procesar la operacion.")
        };
 
        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Excepcion no controlada en {Path}", httpContext.Request.Path);
        }
        else
        {
            _logger.LogWarning(exception, "Excepcion controlada ({StatusCode}) en {Path}", statusCode, httpContext.Request.Path);
        }
 
        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = exception.Message,
            Instance = httpContext.Request.Path
        };
 
        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
 
        return true;
    }
}
 
