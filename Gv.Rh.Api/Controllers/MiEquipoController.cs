using Gv.Rh.Application.DTOs.Empleados;
using Gv.Rh.Application.Interfaces;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gv.Rh.Api.Controllers;

[ApiController]
[Route("api/me/equipo")]
[Authorize(Roles = "ADMIN,RRHH,JEFE")]
public sealed class MiEquipoController : ControllerBase
{
    private readonly RhDbContext _db;
    private readonly IEmpleadoAccessScopeService _empleadoAccessScopeService;

    public MiEquipoController(
        RhDbContext db,
        IEmpleadoAccessScopeService empleadoAccessScopeService)
    {
        _db = db;
        _empleadoAccessScopeService = empleadoAccessScopeService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(MiEquipoDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MiEquipoDto>> Get(CancellationToken cancellationToken)
    {
        var jefeEmpleadoId = await _empleadoAccessScopeService.ResolveCurrentEmpleadoIdAsync(
            User,
            cancellationToken);

        if (!jefeEmpleadoId.HasValue || jefeEmpleadoId.Value <= 0)
        {
            return Forbid();
        }

        var jefe = await _db.Empleados
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == jefeEmpleadoId.Value, cancellationToken);

        if (jefe is null)
        {
            return NotFound(new
            {
                message = "El usuario actual no está ligado a un empleado válido."
            });
        }

        var empleados = await _db.Empleados
            .AsNoTracking()
            .Include(x => x.Sucursal)
            .Include(x => x.Departamento)
            .Include(x => x.Puesto)
            .Where(x =>
                x.Activo &&
                (
                    x.AprobadorPrimarioEmpleadoId == jefeEmpleadoId.Value ||
                    x.AprobadorSecundarioEmpleadoId == jefeEmpleadoId.Value
                ))
            .OrderBy(x => x.ApellidoPaterno)
            .ThenBy(x => x.ApellidoMaterno)
            .ThenBy(x => x.Nombres)
            .Select(x => new MiEquipoEmpleadoDto
            {
                Id = x.Id,
                NumEmpleado = x.NumEmpleado,
                NombreCompleto = x.Nombres + " " + x.ApellidoPaterno +
                    (!string.IsNullOrWhiteSpace(x.ApellidoMaterno)
                        ? " " + x.ApellidoMaterno
                        : ""),
                Email = x.Email,
                Telefono = x.Telefono,
                SucursalId = x.SucursalId,
                SucursalNombre = x.Sucursal != null ? x.Sucursal.Nombre : null,
                DepartamentoId = x.DepartamentoId,
                DepartamentoNombre = x.Departamento != null ? x.Departamento.Nombre : null,
                PuestoId = x.PuestoId,
                PuestoNombre = x.Puesto != null ? x.Puesto.Nombre : null,
                TipoRelacion =
                    x.AprobadorPrimarioEmpleadoId == jefeEmpleadoId.Value
                        ? "APROBADOR_PRIMARIO"
                        : "APROBADOR_SECUNDARIO",
                Activo = x.Activo,
                EstatusLaboral = x.EstatusLaboralActual.ToString()
            })
            .ToListAsync(cancellationToken);

        var dto = new MiEquipoDto
        {
            JefeEmpleadoId = jefe.Id,
            JefeNombre = BuildNombreCompleto(jefe),
            Total = empleados.Count,
            ComoAprobadorPrimario = empleados.Count(x => x.TipoRelacion == "APROBADOR_PRIMARIO"),
            ComoAprobadorSecundario = empleados.Count(x => x.TipoRelacion == "APROBADOR_SECUNDARIO"),
            Empleados = empleados
        };

        return Ok(dto);
    }

    private static string BuildNombreCompleto(Gv.Rh.Domain.Entities.Empleado empleado)
    {
        return string.Join(
            " ",
            new[]
            {
                empleado.Nombres,
                empleado.ApellidoPaterno,
                empleado.ApellidoMaterno
            }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }
}