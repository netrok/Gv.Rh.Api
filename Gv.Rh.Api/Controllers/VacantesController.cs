using Gv.Rh.Application.DTOs.Reclutamiento.Vacantes;
using Gv.Rh.Domain.Common;
using Gv.Rh.Domain.Entities;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gv.Rh.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "ADMIN,RRHH")]
public class VacantesController : ControllerBase
{
    private readonly RhDbContext _db;

    public VacantesController(RhDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<VacanteListItemDto>>> GetAll(
        [FromQuery] string? q,
        [FromQuery] EstatusVacante? estatus,
        [FromQuery] int? departamentoId,
        [FromQuery] int? puestoId,
        [FromQuery] int? sucursalId,
        [FromQuery] bool soloActivas = true,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Vacantes
            .AsNoTracking()
            .Include(x => x.Departamento)
            .Include(x => x.Puesto)
            .Include(x => x.Sucursal)
            .Include(x => x.Postulaciones)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(x => x.Folio.Contains(q) || x.Titulo.Contains(q));
        }

        if (estatus.HasValue)
            query = query.Where(x => x.Estatus == estatus.Value);

        if (departamentoId.HasValue)
            query = query.Where(x => x.DepartamentoId == departamentoId.Value);

        if (puestoId.HasValue)
            query = query.Where(x => x.PuestoId == puestoId.Value);

        if (sucursalId.HasValue)
            query = query.Where(x => x.SucursalId == sucursalId.Value);

        if (soloActivas)
            query = query.Where(x => x.Activo);

        var items = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new VacanteListItemDto
            {
                Id = x.Id,
                Folio = x.Folio,
                Titulo = x.Titulo,
                DepartamentoId = x.DepartamentoId,
                DepartamentoNombre = x.Departamento.Nombre,
                PuestoId = x.PuestoId,
                PuestoNombre = x.Puesto.Nombre,
                SucursalId = x.SucursalId,
                SucursalNombre = x.Sucursal.Nombre,
                NumeroPosiciones = x.NumeroPosiciones,
                PosicionesCubiertas = x.Postulaciones.Count(p => p.EsContratado),
                FechaApertura = x.FechaApertura,
                FechaCierre = x.FechaCierre,
                Estatus = x.Estatus,
                Activo = x.Activo
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<VacanteDetailDto>> GetById(
        int id,
        CancellationToken cancellationToken = default)
    {
        var item = await _db.Vacantes
            .AsNoTracking()
            .Include(x => x.Departamento)
            .Include(x => x.Puesto)
            .Include(x => x.Sucursal)
            .Include(x => x.Postulaciones)
            .Where(x => x.Id == id)
            .Select(x => new VacanteDetailDto
            {
                Id = x.Id,
                Folio = x.Folio,
                Titulo = x.Titulo,
                DepartamentoId = x.DepartamentoId,
                DepartamentoNombre = x.Departamento.Nombre,
                PuestoId = x.PuestoId,
                PuestoNombre = x.Puesto.Nombre,
                SucursalId = x.SucursalId,
                SucursalNombre = x.Sucursal.Nombre,
                NumeroPosiciones = x.NumeroPosiciones,
                Descripcion = x.Descripcion,
                Perfil = x.Perfil,
                SalarioMinimo = x.SalarioMinimo,
                SalarioMaximo = x.SalarioMaximo,
                FechaApertura = x.FechaApertura,
                FechaCierre = x.FechaCierre,
                Estatus = x.Estatus,
                Activo = x.Activo,
                PostulacionesTotal = x.Postulaciones.Count,
                ContratadosTotal = x.Postulaciones.Count(p => p.EsContratado),
                DescartadosTotal = x.Postulaciones.Count(p => p.EtapaActual == EtapaPostulacion.DESCARTADO),
                CreatedAtUtc = x.CreatedAtUtc,
                UpdatedAtUtc = x.UpdatedAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (item is null)
            return NotFound("Vacante no encontrada.");

        return Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<VacanteDetailDto>> Create(
        [FromBody] CreateVacanteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.SalarioMinimo.HasValue && request.SalarioMaximo.HasValue &&
            request.SalarioMinimo.Value > request.SalarioMaximo.Value)
        {
            return BadRequest("El salario mínimo no puede ser mayor al salario máximo.");
        }

        var departamentoExiste = await _db.Departamentos.AnyAsync(x => x.Id == request.DepartamentoId && x.Activo, cancellationToken);
        if (!departamentoExiste)
            return BadRequest("El departamento indicado no existe o está inactivo.");

        var puestoExiste = await _db.Puestos.AnyAsync(x => x.Id == request.PuestoId && x.Activo, cancellationToken);
        if (!puestoExiste)
            return BadRequest("El puesto indicado no existe o está inactivo.");

        var sucursalExiste = await _db.Sucursales.AnyAsync(x => x.Id == request.SucursalId && x.Activo, cancellationToken);
        if (!sucursalExiste)
            return BadRequest("La sucursal indicada no existe o está inactiva.");
        
        var nextValue = await _db.Database
            .SqlQuery<int>($@"select nextval('vacantes_folio_seq')::int as ""Value""")
            .SingleAsync(cancellationToken);

        var folio = $"VAC-{DateTime.UtcNow:yyyy}-{nextValue:000000}";

        var entity = new Vacante
        {
            Folio = folio,
            Titulo = request.Titulo.Trim(),
            DepartamentoId = request.DepartamentoId,
            PuestoId = request.PuestoId,
            SucursalId = request.SucursalId,
            NumeroPosiciones = request.NumeroPosiciones,
            Descripcion = string.IsNullOrWhiteSpace(request.Descripcion) ? null : request.Descripcion.Trim(),
            Perfil = string.IsNullOrWhiteSpace(request.Perfil) ? null : request.Perfil.Trim(),
            SalarioMinimo = request.SalarioMinimo,
            SalarioMaximo = request.SalarioMaximo,
            FechaApertura = request.FechaApertura,
            Estatus = request.CrearComoBorrador ? EstatusVacante.BORRADOR : EstatusVacante.ABIERTA,
            Activo = request.Activo,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Vacantes.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, new { entity.Id, entity.Folio });
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult> Update(
        int id,
        [FromBody] UpdateVacanteRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.Vacantes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
            return NotFound("Vacante no encontrada.");

        if (request.SalarioMinimo.HasValue && request.SalarioMaximo.HasValue &&
            request.SalarioMinimo.Value > request.SalarioMaximo.Value)
        {
            return BadRequest("El salario mínimo no puede ser mayor al salario máximo.");
        }

        var departamentoExiste = await _db.Departamentos.AnyAsync(x => x.Id == request.DepartamentoId && x.Activo, cancellationToken);
        if (!departamentoExiste)
            return BadRequest("El departamento indicado no existe o está inactivo.");

        var puestoExiste = await _db.Puestos.AnyAsync(x => x.Id == request.PuestoId && x.Activo, cancellationToken);
        if (!puestoExiste)
            return BadRequest("El puesto indicado no existe o está inactivo.");

        var sucursalExiste = await _db.Sucursales.AnyAsync(x => x.Id == request.SucursalId && x.Activo, cancellationToken);
        if (!sucursalExiste)
            return BadRequest("La sucursal indicada no existe o está inactiva.");

        entity.Titulo = request.Titulo.Trim();
        entity.DepartamentoId = request.DepartamentoId;
        entity.PuestoId = request.PuestoId;
        entity.SucursalId = request.SucursalId;
        entity.NumeroPosiciones = request.NumeroPosiciones;
        entity.Descripcion = string.IsNullOrWhiteSpace(request.Descripcion) ? null : request.Descripcion.Trim();
        entity.Perfil = string.IsNullOrWhiteSpace(request.Perfil) ? null : request.Perfil.Trim();
        entity.SalarioMinimo = request.SalarioMinimo;
        entity.SalarioMaximo = request.SalarioMaximo;
        entity.FechaApertura = request.FechaApertura;
        entity.Activo = request.Activo;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpPost("{id:int}/abrir")]
    public async Task<ActionResult> Abrir(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Vacantes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
            return NotFound("Vacante no encontrada.");

        if (entity.Estatus == EstatusVacante.CERRADA || entity.Estatus == EstatusVacante.CANCELADA)
            return BadRequest("No se puede abrir una vacante cerrada o cancelada.");

        entity.Estatus = EstatusVacante.ABIERTA;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/pausar")]
    public async Task<ActionResult> Pausar(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Vacantes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
            return NotFound("Vacante no encontrada.");

        if (entity.Estatus != EstatusVacante.ABIERTA)
            return BadRequest("Solo se puede pausar una vacante abierta.");

        entity.Estatus = EstatusVacante.PAUSADA;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/cerrar")]
    public async Task<ActionResult> Cerrar(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Vacantes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
            return NotFound("Vacante no encontrada.");

        if (entity.Estatus == EstatusVacante.CANCELADA)
            return BadRequest("No se puede cerrar una vacante cancelada.");

        entity.Estatus = EstatusVacante.CERRADA;
        entity.FechaCierre ??= DateOnly.FromDateTime(DateTime.UtcNow);
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/cancelar")]
    public async Task<ActionResult> Cancelar(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Vacantes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
            return NotFound("Vacante no encontrada.");

        if (entity.Estatus == EstatusVacante.CERRADA)
            return BadRequest("No se puede cancelar una vacante ya cerrada.");

        entity.Estatus = EstatusVacante.CANCELADA;
        entity.FechaCierre ??= DateOnly.FromDateTime(DateTime.UtcNow);
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}