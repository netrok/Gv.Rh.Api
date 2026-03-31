using System.Security.Claims;
using Gv.Rh.Application.DTOs.Reclutamiento.Postulaciones;
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
public class PostulacionesController : ControllerBase
{
    private readonly RhDbContext _db;

    public PostulacionesController(RhDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<PostulacionListItemDto>>> GetAll(
        [FromQuery] string? q,
        [FromQuery] int? vacanteId,
        [FromQuery] EtapaPostulacion? etapa,
        [FromQuery] bool? esContratado,
        [FromQuery] bool soloActivas = true,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Postulaciones
            .AsNoTracking()
            .Include(x => x.Vacante)
            .Include(x => x.Candidato)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(x =>
                x.Vacante.Folio.Contains(q) ||
                x.Vacante.Titulo.Contains(q) ||
                x.Candidato.Nombres.Contains(q) ||
                x.Candidato.ApellidoPaterno.Contains(q) ||
                (x.Candidato.ApellidoMaterno != null && x.Candidato.ApellidoMaterno.Contains(q)));
        }

        if (vacanteId.HasValue)
            query = query.Where(x => x.VacanteId == vacanteId.Value);

        if (etapa.HasValue)
            query = query.Where(x => x.EtapaActual == etapa.Value);

        if (esContratado.HasValue)
            query = query.Where(x => x.EsContratado == esContratado.Value);

        if (soloActivas)
            query = query.Where(x => x.Activo);

        var items = await query
            .OrderByDescending(x => x.FechaUltimoMovimientoUtc)
            .Select(x => new PostulacionListItemDto
            {
                Id = x.Id,
                VacanteId = x.VacanteId,
                VacanteFolio = x.Vacante.Folio,
                VacanteTitulo = x.Vacante.Titulo,
                CandidatoId = x.CandidatoId,
                CandidatoNombre = (x.Candidato.Nombres + " " + x.Candidato.ApellidoPaterno + " " + (x.Candidato.ApellidoMaterno ?? "")).Trim(),
                EtapaActual = x.EtapaActual,
                FechaPostulacionUtc = x.FechaPostulacionUtc,
                FechaUltimoMovimientoUtc = x.FechaUltimoMovimientoUtc,
                EsContratado = x.EsContratado,
                EmpleadoId = x.EmpleadoId,
                Activo = x.Activo
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PostulacionDetailDto>> GetById(
        int id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.Postulaciones
            .AsNoTracking()
            .Include(x => x.Vacante)
            .Include(x => x.Candidato)
            .Include(x => x.Seguimientos)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Postulación no encontrada.");

        var result = new PostulacionDetailDto
        {
            Id = entity.Id,
            VacanteId = entity.VacanteId,
            VacanteFolio = entity.Vacante.Folio,
            VacanteTitulo = entity.Vacante.Titulo,
            CandidatoId = entity.CandidatoId,
            CandidatoNombre = (entity.Candidato.Nombres + " " + entity.Candidato.ApellidoPaterno + " " + (entity.Candidato.ApellidoMaterno ?? "")).Trim(),
            CandidatoTelefono = entity.Candidato.Telefono,
            CandidatoEmail = entity.Candidato.Email,
            EtapaActual = entity.EtapaActual,
            FechaPostulacionUtc = entity.FechaPostulacionUtc,
            FechaUltimoMovimientoUtc = entity.FechaUltimoMovimientoUtc,
            ObservacionesInternas = entity.ObservacionesInternas,
            MotivoDescarte = entity.MotivoDescarte,
            EsContratado = entity.EsContratado,
            EmpleadoId = entity.EmpleadoId,
            FechaContratacionUtc = entity.FechaContratacionUtc,
            Activo = entity.Activo,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc,
            Seguimiento = entity.Seguimientos
                .OrderByDescending(s => s.CreatedAtUtc)
                .Select(s => new PostulacionSeguimientoDto
                {
                    Id = s.Id,
                    EtapaAnterior = s.EtapaAnterior,
                    EtapaNueva = s.EtapaNueva,
                    Comentario = s.Comentario,
                    CreadoPorUserId = s.CreadoPorUserId,
                    CreatedAtUtc = s.CreatedAtUtc
                })
                .ToList()
        };

        return Ok(result);
    }

    [HttpGet("{id:int}/seguimiento")]
    public async Task<ActionResult<List<PostulacionSeguimientoDto>>> GetSeguimiento(
        int id,
        CancellationToken cancellationToken = default)
    {
        var existe = await _db.Postulaciones.AnyAsync(x => x.Id == id, cancellationToken);
        if (!existe)
            return NotFound("Postulación no encontrada.");

        var items = await _db.PostulacionesSeguimientos
            .AsNoTracking()
            .Where(x => x.PostulacionId == id)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new PostulacionSeguimientoDto
            {
                Id = x.Id,
                EtapaAnterior = x.EtapaAnterior,
                EtapaNueva = x.EtapaNueva,
                Comentario = x.Comentario,
                CreadoPorUserId = x.CreadoPorUserId,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult> Create(
        [FromBody] CreatePostulacionRequest request,
        CancellationToken cancellationToken = default)
    {
        var vacante = await _db.Vacantes.FirstOrDefaultAsync(x => x.Id == request.VacanteId, cancellationToken);
        if (vacante is null)
            return BadRequest("La vacante indicada no existe.");

        if (!vacante.Activo || vacante.Estatus != EstatusVacante.ABIERTA)
            return BadRequest("La vacante no está abierta para recibir postulaciones.");

        var candidato = await _db.Candidatos.FirstOrDefaultAsync(x => x.Id == request.CandidatoId, cancellationToken);
        if (candidato is null)
            return BadRequest("El candidato indicado no existe.");

        if (!candidato.Activo)
            return BadRequest("El candidato está inactivo.");

        var duplicada = await _db.Postulaciones.AnyAsync(
            x => x.VacanteId == request.VacanteId &&
                 x.CandidatoId == request.CandidatoId &&
                 x.Activo,
            cancellationToken);

        if (duplicada)
            return BadRequest("El candidato ya tiene una postulación activa para esta vacante.");

        var userId = GetCurrentUserId();

        var entity = new Postulacion
        {
            VacanteId = request.VacanteId,
            CandidatoId = request.CandidatoId,
            EtapaActual = EtapaPostulacion.POSTULADO,
            FechaPostulacionUtc = DateTime.UtcNow,
            FechaUltimoMovimientoUtc = DateTime.UtcNow,
            ObservacionesInternas = string.IsNullOrWhiteSpace(request.ObservacionesInternas) ? null : request.ObservacionesInternas.Trim(),
            Activo = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        entity.Seguimientos.Add(new PostulacionSeguimiento
        {
            EtapaAnterior = null,
            EtapaNueva = EtapaPostulacion.POSTULADO,
            Comentario = "Postulación creada.",
            CreadoPorUserId = userId,
            CreatedAtUtc = DateTime.UtcNow
        });

        _db.Postulaciones.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, new { entity.Id });
    }

    [HttpPost("{id:int}/mover-etapa")]
    public async Task<ActionResult> MoverEtapa(
        int id,
        [FromBody] MoverPostulacionEtapaRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.Postulaciones.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
            return NotFound("Postulación no encontrada.");

        if (!entity.Activo)
            return BadRequest("La postulación está inactiva.");

        if (entity.EtapaActual == EtapaPostulacion.DESCARTADO || entity.EtapaActual == EtapaPostulacion.CONTRATADO)
            return BadRequest("No puedes mover con este endpoint una postulación descartada o contratada.");

        if (request.EtapaNueva == EtapaPostulacion.DESCARTADO || request.EtapaNueva == EtapaPostulacion.CONTRATADO)
            return BadRequest("Usa los endpoints específicos para descartar o contratar.");

        if (request.EtapaNueva == entity.EtapaActual)
            return BadRequest("La postulación ya está en esa etapa.");

        var etapaAnterior = entity.EtapaActual;
        entity.EtapaActual = request.EtapaNueva;
        entity.FechaUltimoMovimientoUtc = DateTime.UtcNow;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        _db.PostulacionesSeguimientos.Add(new PostulacionSeguimiento
        {
            PostulacionId = entity.Id,
            EtapaAnterior = etapaAnterior,
            EtapaNueva = request.EtapaNueva,
            Comentario = string.IsNullOrWhiteSpace(request.Comentario) ? null : request.Comentario.Trim(),
            CreadoPorUserId = GetCurrentUserId(),
            CreatedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/descartar")]
    public async Task<ActionResult> Descartar(
        int id,
        [FromBody] DescartarPostulacionRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.Postulaciones.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
            return NotFound("Postulación no encontrada.");

        if (!entity.Activo)
            return BadRequest("La postulación está inactiva.");

        if (entity.EtapaActual == EtapaPostulacion.DESCARTADO)
            return BadRequest("La postulación ya fue descartada.");

        if (entity.EsContratado)
            return BadRequest("No puedes descartar una postulación ya contratada.");

        var etapaAnterior = entity.EtapaActual;

        entity.EtapaActual = EtapaPostulacion.DESCARTADO;
        entity.MotivoDescarte = request.MotivoDescarte.Trim();
        entity.FechaUltimoMovimientoUtc = DateTime.UtcNow;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        _db.PostulacionesSeguimientos.Add(new PostulacionSeguimiento
        {
            PostulacionId = entity.Id,
            EtapaAnterior = etapaAnterior,
            EtapaNueva = EtapaPostulacion.DESCARTADO,
            Comentario = string.IsNullOrWhiteSpace(request.Comentario) ? request.MotivoDescarte.Trim() : request.Comentario.Trim(),
            CreadoPorUserId = GetCurrentUserId(),
            CreatedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/contratar")]
    public async Task<ActionResult> Contratar(
        int id,
        [FromBody] ContratarPostulacionRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.Postulaciones.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
            return NotFound("Postulación no encontrada.");

        if (!entity.Activo)
            return BadRequest("La postulación está inactiva.");

        if (entity.EtapaActual == EtapaPostulacion.DESCARTADO)
            return BadRequest("No puedes contratar una postulación descartada.");

        if (entity.EsContratado)
            return BadRequest("La postulación ya fue marcada como contratada.");

        var etapaAnterior = entity.EtapaActual;

        entity.EtapaActual = EtapaPostulacion.CONTRATADO;
        entity.EsContratado = true;
        entity.FechaContratacionUtc = DateTime.UtcNow;
        entity.FechaUltimoMovimientoUtc = DateTime.UtcNow;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        _db.PostulacionesSeguimientos.Add(new PostulacionSeguimiento
        {
            PostulacionId = entity.Id,
            EtapaAnterior = etapaAnterior,
            EtapaNueva = EtapaPostulacion.CONTRATADO,
            Comentario = string.IsNullOrWhiteSpace(request.Comentario) ? "Postulación marcada como contratada." : request.Comentario.Trim(),
            CreadoPorUserId = GetCurrentUserId(),
            CreatedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/convertir-a-empleado")]
    public async Task<ActionResult> ConvertirAEmpleado(
        int id,
        [FromBody] ConvertirPostulacionAEmpleadoRequest request,
        CancellationToken cancellationToken = default)
    {
        var postulacion = await _db.Postulaciones
            .Include(x => x.Candidato)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (postulacion is null)
            return NotFound("Postulación no encontrada.");

        if (!postulacion.Activo)
            return BadRequest("La postulación está inactiva.");

        if (postulacion.EtapaActual != EtapaPostulacion.CONTRATADO || !postulacion.EsContratado)
            return BadRequest("Solo una postulación contratada puede convertirse a empleado.");

        if (postulacion.EmpleadoId.HasValue)
            return BadRequest("La postulación ya fue convertida a empleado.");

        var departamentoExiste = await _db.Departamentos.AnyAsync(x => x.Id == request.DepartamentoId && x.Activo, cancellationToken);
        if (!departamentoExiste)
            return BadRequest("El departamento indicado no existe o está inactivo.");

        var puestoExiste = await _db.Puestos.AnyAsync(x => x.Id == request.PuestoId && x.Activo, cancellationToken);
        if (!puestoExiste)
            return BadRequest("El puesto indicado no existe o está inactivo.");

        var sucursalExiste = await _db.Sucursales.AnyAsync(x => x.Id == request.SucursalId && x.Activo, cancellationToken);
        if (!sucursalExiste)
            return BadRequest("La sucursal indicada no existe o está inactiva.");

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        // IMPORTANTE:
        // Reusa aquí EXACTAMENTE la misma lógica de generación de NumEmpleado
        // que ya utiliza tu módulo actual de Empleados.
        // Si ya tienes helper o service, úsalo aquí.
        var nextEmpleadoValue = await _db.Database
    .SqlQuery<int>($@"select nextval('empleados_num_seq')::int as ""Value""")
    .SingleAsync(cancellationToken);

        var empleado = new Empleado
        {
            NumEmpleado = $"EMP-{nextEmpleadoValue:000000}",
            Nombres = postulacion.Candidato.Nombres,
            ApellidoPaterno = postulacion.Candidato.ApellidoPaterno,
            ApellidoMaterno = postulacion.Candidato.ApellidoMaterno,
            Telefono = postulacion.Candidato.Telefono,
            Email = postulacion.Candidato.Email,
            FechaIngreso = request.FechaIngreso,
            Activo = request.Activo,
            DepartamentoId = request.DepartamentoId,
            PuestoId = request.PuestoId,
            SucursalId = request.SucursalId
        };

        _db.Empleados.Add(empleado);
        await _db.SaveChangesAsync(cancellationToken);

        postulacion.EmpleadoId = empleado.Id;
        postulacion.UpdatedAtUtc = DateTime.UtcNow;
        postulacion.FechaUltimoMovimientoUtc = DateTime.UtcNow;

        _db.PostulacionesSeguimientos.Add(new PostulacionSeguimiento
        {
            PostulacionId = postulacion.Id,
            EtapaAnterior = EtapaPostulacion.CONTRATADO,
            EtapaNueva = EtapaPostulacion.CONTRATADO,
            Comentario = $"Postulación convertida a empleado con ID {empleado.Id}.",
            CreadoPorUserId = GetCurrentUserId(),
            CreatedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return Ok(new
        {
            empleado.Id,
            empleado.NumEmpleado,
            postulacionId = postulacion.Id
        });
    }

    private int? GetCurrentUserId()
    {
        var raw =
            User.FindFirstValue("sub") ??
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        return int.TryParse(raw, out var userId) ? userId : null;
    }
}