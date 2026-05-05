using Gv.Rh.Application.DTOs.Vacaciones;
using Gv.Rh.Application.Interfaces;
using Gv.Rh.Domain.Common;
using Gv.Rh.Domain.Entities;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Gv.Rh.Infrastructure.Services;

public class VacacionesService : IVacacionesService
{
    private readonly RhDbContext _db;

    public VacacionesService(RhDbContext db)
    {
        _db = db;
    }

    public async Task<VacacionesResumenDto> GetResumenAsync(
        int empleadoId,
        CancellationToken cancellationToken = default)
    {
        var empleado = await _db.Empleados
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == empleadoId, cancellationToken);

        if (empleado is null)
            throw new KeyNotFoundException("El empleado no existe.");

        var cicloInfo = await ResolveCicloLaboralActualAsync(empleado, cancellationToken);
        var politica = await GetPoliticaVigenteAsync(cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.Today);

        var periodos = await _db.VacacionPeriodos
            .AsNoTracking()
            .Include(x => x.VacacionPolitica)
            .Where(x => x.EmpleadoId == empleadoId)
            .OrderByDescending(x => x.CicloLaboral)
            .ThenByDescending(x => x.AnioServicio)
            .ThenByDescending(x => x.FechaInicio)
            .ToListAsync(cancellationToken);

        var periodosCicloActual = periodos
            .Where(x => x.CicloLaboral == cicloInfo.CicloLaboral)
            .ToList();

        var periodoActual = periodosCicloActual
            .OrderByDescending(x => x.AnioServicio)
            .FirstOrDefault(x => x.Estatus == EstatusVacacionPeriodo.ABIERTO)
            ?? periodosCicloActual
                .OrderByDescending(x => x.AnioServicio)
                .FirstOrDefault();

        return new VacacionesResumenDto
        {
            EmpleadoId = empleado.Id,
            NumEmpleado = empleado.NumEmpleado,
            EmpleadoNombre = BuildNombreEmpleado(empleado),

            // Para vacaciones usamos la fecha base del ciclo laboral actual.
            // Ciclo 1 = FechaIngreso. Ciclo 2+ = último reingreso.
            FechaIngreso = cicloInfo.FechaBase,

            AntiguedadAnios = CalcularAniosServicioCumplidos(cicloInfo.FechaBase, today),
            ProximoAniversario = CalcularProximoAniversario(cicloInfo.FechaBase, today),

            PoliticaId = politica.Id,
            PoliticaNombre = politica.Nombre,
            PrimaVacacionalPorcentaje = politica.PrimaVacacionalPorcentaje,

            DiasDerechoTotal = periodosCicloActual.Sum(x => x.DiasDerecho),
            DiasTomadosTotal = periodosCicloActual.Sum(x => x.DiasTomados),
            DiasPagadosTotal = periodosCicloActual.Sum(x => x.DiasPagados),
            DiasAjustadosTotal = periodosCicloActual.Sum(x => x.DiasAjustados),
            DiasVencidosTotal = periodosCicloActual.Sum(x => x.DiasVencidos),
            SaldoDisponible = periodosCicloActual.Sum(x => x.Saldo),
            PeriodosTotales = periodosCicloActual.Count,
            PeriodosAbiertos = periodosCicloActual.Count(x => x.Estatus == EstatusVacacionPeriodo.ABIERTO),
            PeriodoActual = periodoActual is null ? null : ToPeriodoDto(periodoActual)
        };
    }

    public async Task<List<VacacionPeriodoDto>> GetPeriodosAsync(
        int empleadoId,
        CancellationToken cancellationToken = default)
    {
        await EnsureEmpleadoExistsAsync(empleadoId, cancellationToken);

        return await _db.VacacionPeriodos
            .AsNoTracking()
            .Include(x => x.VacacionPolitica)
            .Where(x => x.EmpleadoId == empleadoId)
            .OrderByDescending(x => x.CicloLaboral)
            .ThenByDescending(x => x.AnioServicio)
            .ThenByDescending(x => x.FechaInicio)
            .Select(x => ToPeriodoDto(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<VacacionMovimientoDto>> GetKardexAsync(
        int empleadoId,
        CancellationToken cancellationToken = default)
    {
        await EnsureEmpleadoExistsAsync(empleadoId, cancellationToken);

        return await _db.VacacionMovimientos
            .AsNoTracking()
            .Include(x => x.VacacionPeriodo)
            .Where(x => x.EmpleadoId == empleadoId)
            .OrderByDescending(x => x.CicloLaboral)
            .ThenByDescending(x => x.FechaMovimiento)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Select(x => ToMovimientoDto(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<VacacionPeriodoDto> GenerarPeriodoAsync(
        int empleadoId,
        VacacionGenerarPeriodoRequestDto request,
        int? usuarioResponsableId,
        CancellationToken cancellationToken = default)
    {
        var empleado = await _db.Empleados
            .FirstOrDefaultAsync(x => x.Id == empleadoId, cancellationToken);

        if (empleado is null)
            throw new KeyNotFoundException("El empleado no existe.");

        if (!empleado.Activo)
            throw new InvalidOperationException("No se puede generar periodo de vacaciones para un empleado inactivo.");

        var cicloInfo = await ResolveCicloLaboralActualAsync(empleado, cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var aniosCumplidos = CalcularAniosServicioCumplidos(cicloInfo.FechaBase, today);

        if (aniosCumplidos < 1 && !request.AnioServicio.HasValue)
            throw new InvalidOperationException("El empleado aún no cumple un año de servicio en el ciclo laboral actual.");

        var anioServicio = request.AnioServicio ?? aniosCumplidos;

        if (anioServicio < 1)
            throw new InvalidOperationException("El año de servicio debe ser mayor o igual a 1.");

        var existente = await _db.VacacionPeriodos
            .Include(x => x.VacacionPolitica)
            .FirstOrDefaultAsync(
                x =>
                    x.EmpleadoId == empleadoId &&
                    x.CicloLaboral == cicloInfo.CicloLaboral &&
                    x.AnioServicio == anioServicio,
                cancellationToken);

        if (existente is not null)
            return ToPeriodoDto(existente);

        var politica = await GetPoliticaVigenteAsync(cancellationToken);
        var diasDerecho = ResolverDiasVacaciones(politica, anioServicio);

        var fechaInicio = cicloInfo.FechaBase.AddYears(anioServicio - 1);
        var fechaFin = cicloInfo.FechaBase.AddYears(anioServicio).AddDays(-1);
        var fechaLimiteDisfrute = fechaFin.AddMonths(6);
        var now = DateTime.UtcNow;

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        var periodo = new VacacionPeriodo
        {
            EmpleadoId = empleadoId,
            VacacionPoliticaId = politica.Id,
            CicloLaboral = cicloInfo.CicloLaboral,
            AnioServicio = anioServicio,
            FechaInicio = fechaInicio,
            FechaFin = fechaFin,
            FechaLimiteDisfrute = fechaLimiteDisfrute,
            DiasDerecho = diasDerecho,
            DiasTomados = 0m,
            DiasPagados = 0m,
            DiasAjustados = 0m,
            DiasVencidos = 0m,
            Saldo = diasDerecho,
            PrimaPagada = false,
            Comentario = NormalizeNullable(request.Comentario),
            Estatus = EstatusVacacionPeriodo.ABIERTO,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.VacacionPeriodos.Add(periodo);
        await _db.SaveChangesAsync(cancellationToken);

        var movimiento = new VacacionMovimiento
        {
            EmpleadoId = empleadoId,
            VacacionPeriodoId = periodo.Id,
            CicloLaboral = periodo.CicloLaboral,
            TipoMovimiento = TipoMovimientoVacacion.APERTURA,
            FechaMovimiento = fechaFin,
            Dias = diasDerecho,
            SaldoAntes = 0m,
            SaldoDespues = diasDerecho,
            Referencia = $"CICLO-{periodo.CicloLaboral}-PERIODO-{anioServicio}",
            Comentario = NormalizeNullable(request.Comentario)
                ?? $"Apertura automática del periodo de vacaciones ciclo {periodo.CicloLaboral}, año servicio {anioServicio}.",
            UsuarioResponsableId = usuarioResponsableId,
            Origen = "SISTEMA",
            CreatedAtUtc = now
        };

        _db.VacacionMovimientos.Add(movimiento);
        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        periodo.VacacionPolitica = politica;
        return ToPeriodoDto(periodo);
    }

    public async Task<VacacionMovimientoDto> RegistrarDisfruteAsync(
        int empleadoId,
        VacacionRegistrarDisfruteRequestDto request,
        int? usuarioResponsableId,
        CancellationToken cancellationToken = default)
    {
        if (request.Dias <= 0)
            throw new InvalidOperationException("Los días disfrutados deben ser mayores a cero.");

        if (request.FechaFinDisfrute < request.FechaInicioDisfrute)
            throw new InvalidOperationException("La fecha final de disfrute no puede ser menor a la fecha inicial.");

        var periodo = await GetPeriodoForMutationAsync(empleadoId, request.VacacionPeriodoId, cancellationToken);

        if (periodo.Saldo < request.Dias)
            throw new InvalidOperationException("El periodo no tiene saldo suficiente para registrar el disfrute.");

        var now = DateTime.UtcNow;
        var saldoAntes = periodo.Saldo;
        var saldoDespues = saldoAntes - request.Dias;

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        periodo.DiasTomados += request.Dias;
        periodo.Saldo = saldoDespues;
        periodo.UpdatedAtUtc = now;

        var movimiento = new VacacionMovimiento
        {
            EmpleadoId = empleadoId,
            VacacionPeriodoId = periodo.Id,
            CicloLaboral = periodo.CicloLaboral,
            TipoMovimiento = TipoMovimientoVacacion.DISFRUTE,
            FechaMovimiento = request.FechaInicioDisfrute,
            FechaInicioDisfrute = request.FechaInicioDisfrute,
            FechaFinDisfrute = request.FechaFinDisfrute,
            Dias = -request.Dias,
            SaldoAntes = saldoAntes,
            SaldoDespues = saldoDespues,
            Referencia = NormalizeNullable(request.Referencia),
            Comentario = NormalizeNullable(request.Comentario),
            UsuarioResponsableId = usuarioResponsableId,
            Origen = "MANUAL",
            CreatedAtUtc = now
        };

        _db.VacacionMovimientos.Add(movimiento);
        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        movimiento.VacacionPeriodo = periodo;
        return ToMovimientoDto(movimiento);
    }

    public async Task<VacacionMovimientoDto> RegistrarAjusteAsync(
        int empleadoId,
        VacacionAjusteRequestDto request,
        int? usuarioResponsableId,
        CancellationToken cancellationToken = default)
    {
        if (request.Dias == 0)
            throw new InvalidOperationException("El ajuste debe ser diferente de cero.");

        var periodo = await GetPeriodoForMutationAsync(empleadoId, request.VacacionPeriodoId, cancellationToken);

        var now = DateTime.UtcNow;
        var saldoAntes = periodo.Saldo;
        var saldoDespues = saldoAntes + request.Dias;

        if (saldoDespues < 0)
            throw new InvalidOperationException("El ajuste dejaría el saldo del periodo en negativo.");

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        periodo.DiasAjustados += request.Dias;
        periodo.Saldo = saldoDespues;
        periodo.UpdatedAtUtc = now;

        var movimiento = new VacacionMovimiento
        {
            EmpleadoId = empleadoId,
            VacacionPeriodoId = periodo.Id,
            CicloLaboral = periodo.CicloLaboral,
            TipoMovimiento = request.Dias > 0
                ? TipoMovimientoVacacion.AJUSTE_POSITIVO
                : TipoMovimientoVacacion.AJUSTE_NEGATIVO,
            FechaMovimiento = request.FechaMovimiento ?? DateOnly.FromDateTime(DateTime.Today),
            Dias = request.Dias,
            SaldoAntes = saldoAntes,
            SaldoDespues = saldoDespues,
            Referencia = NormalizeNullable(request.Referencia),
            Comentario = NormalizeNullable(request.Comentario),
            UsuarioResponsableId = usuarioResponsableId,
            Origen = "MANUAL",
            CreatedAtUtc = now
        };

        _db.VacacionMovimientos.Add(movimiento);
        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        movimiento.VacacionPeriodo = periodo;
        return ToMovimientoDto(movimiento);
    }


    public async Task<int> CerrarPeriodosAbiertosPorBajaAsync(
        int empleadoId,
        DateOnly fechaBaja,
        int? usuarioResponsableId,
        CancellationToken cancellationToken = default)
    {
        var periodos = await _db.VacacionPeriodos
            .Where(x =>
                x.EmpleadoId == empleadoId &&
                x.Estatus == EstatusVacacionPeriodo.ABIERTO)
            .OrderBy(x => x.CicloLaboral)
            .ThenBy(x => x.AnioServicio)
            .ToListAsync(cancellationToken);

        if (periodos.Count == 0)
            return 0;

        var now = DateTime.UtcNow;

        foreach (var periodo in periodos)
        {
            var saldoAntes = periodo.Saldo;

            var comentarioCierre =
                $"Periodo cerrado automáticamente por baja del empleado el {fechaBaja:yyyy-MM-dd}. " +
                $"Saldo congelado: {periodo.Saldo:0.##} días.";

            periodo.Estatus = EstatusVacacionPeriodo.CERRADO;
            periodo.Comentario = AppendVacacionComentario(periodo.Comentario, comentarioCierre);
            periodo.UpdatedAtUtc = now;

            _db.VacacionMovimientos.Add(new VacacionMovimiento
            {
                EmpleadoId = empleadoId,
                VacacionPeriodoId = periodo.Id,
                CicloLaboral = periodo.CicloLaboral,
                TipoMovimiento = TipoMovimientoVacacion.CANCELACION,
                FechaMovimiento = fechaBaja,
                FechaInicioDisfrute = null,
                FechaFinDisfrute = null,
                Dias = 0m,
                SaldoAntes = saldoAntes,
                SaldoDespues = periodo.Saldo,
                Referencia = $"CICLO-{periodo.CicloLaboral}-BAJA",
                Comentario = comentarioCierre,
                UsuarioResponsableId = usuarioResponsableId,
                Origen = "SISTEMA",
                CreatedAtUtc = now
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        return periodos.Count;
    }
    private async Task EnsureEmpleadoExistsAsync(int empleadoId, CancellationToken cancellationToken)
    {
        var exists = await _db.Empleados
            .AsNoTracking()
            .AnyAsync(x => x.Id == empleadoId, cancellationToken);

        if (!exists)
            throw new KeyNotFoundException("El empleado no existe.");
    }

    private async Task<VacacionPeriodo> GetPeriodoForMutationAsync(
        int empleadoId,
        int periodoId,
        CancellationToken cancellationToken)
    {
        var periodo = await _db.VacacionPeriodos
            .FirstOrDefaultAsync(
                x => x.Id == periodoId && x.EmpleadoId == empleadoId,
                cancellationToken);

        if (periodo is null)
            throw new KeyNotFoundException("El periodo de vacaciones no existe para este empleado.");

        if (periodo.Estatus != EstatusVacacionPeriodo.ABIERTO)
            throw new InvalidOperationException("Solo se pueden registrar movimientos en periodos abiertos.");

        return periodo;
    }

    private async Task<VacacionCicloLaboralInfo> ResolveCicloLaboralActualAsync(
        Empleado empleado,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var reingresos = await _db.EmpleadoMovimientosLaborales
            .AsNoTracking()
            .Where(x =>
                x.EmpleadoId == empleado.Id &&
                x.TipoMovimiento == TipoMovimientoLaboral.REINGRESO &&
                x.FechaMovimiento <= today)
            .OrderBy(x => x.FechaMovimiento)
            .ThenBy(x => x.Id)
            .Select(x => x.FechaMovimiento)
            .ToListAsync(cancellationToken);

        var cicloLaboral = reingresos.Count + 1;
        var fechaBase = reingresos.LastOrDefault();

        if (fechaBase == default)
            fechaBase = empleado.FechaIngreso;

        return new VacacionCicloLaboralInfo(cicloLaboral, fechaBase);
    }

    private async Task<VacacionPolitica> GetPoliticaVigenteAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var politica = await _db.VacacionPoliticas
            .AsNoTracking()
            .Include(x => x.Detalles)
            .Where(x => x.Activo)
            .Where(x => x.VigenteDesde <= today)
            .Where(x => !x.VigenteHasta.HasValue || x.VigenteHasta.Value >= today)
            .OrderByDescending(x => x.VigenteDesde)
            .FirstOrDefaultAsync(cancellationToken);

        return politica ?? throw new InvalidOperationException("No existe una política de vacaciones vigente.");
    }

    private static decimal ResolverDiasVacaciones(VacacionPolitica politica, int anioServicio)
    {
        var detalle = politica.Detalles
            .OrderBy(x => x.AnioServicioDesde)
            .FirstOrDefault(x =>
                x.AnioServicioDesde <= anioServicio &&
                (!x.AnioServicioHasta.HasValue || x.AnioServicioHasta.Value >= anioServicio));

        if (detalle is null)
            throw new InvalidOperationException($"No hay días configurados para el año de servicio {anioServicio}.");

        return detalle.DiasVacaciones;
    }

    private static int CalcularAniosServicioCumplidos(DateOnly fechaIngreso, DateOnly fechaReferencia)
    {
        var years = fechaReferencia.Year - fechaIngreso.Year;
        var anniversary = fechaIngreso.AddYears(years);

        if (anniversary > fechaReferencia)
            years--;

        return Math.Max(0, years);
    }

    private static DateOnly CalcularProximoAniversario(DateOnly fechaIngreso, DateOnly fechaReferencia)
    {
        var years = Math.Max(0, fechaReferencia.Year - fechaIngreso.Year);
        var aniversario = fechaIngreso.AddYears(years);

        if (aniversario <= fechaReferencia)
            aniversario = aniversario.AddYears(1);

        return aniversario;
    }

    private static string BuildNombreEmpleado(Empleado empleado)
    {
        var parts = new[]
        {
            empleado.Nombres?.Trim(),
            empleado.ApellidoPaterno?.Trim(),
            empleado.ApellidoMaterno?.Trim()
        }
        .Where(x => !string.IsNullOrWhiteSpace(x));

        return string.Join(" ", parts);
    }


    private static string? AppendVacacionComentario(string? comentarioActual, string comentarioNuevo)
    {
        var actual = NormalizeNullable(comentarioActual);
        var nuevo = NormalizeNullable(comentarioNuevo);

        if (nuevo is null)
            return actual;

        var result = actual is null
            ? nuevo
            : $"{actual} | {nuevo}";

        return result.Length <= 1000
            ? result
            : result[..1000];
    }
    private static string? NormalizeNullable(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static VacacionPeriodoDto ToPeriodoDto(VacacionPeriodo entity)
    {
        return new VacacionPeriodoDto
        {
            Id = entity.Id,
            EmpleadoId = entity.EmpleadoId,
            VacacionPoliticaId = entity.VacacionPoliticaId,
            VacacionPoliticaNombre = entity.VacacionPolitica?.Nombre,
            CicloLaboral = entity.CicloLaboral,
            AnioServicio = entity.AnioServicio,
            FechaInicio = entity.FechaInicio,
            FechaFin = entity.FechaFin,
            FechaLimiteDisfrute = entity.FechaLimiteDisfrute,
            DiasDerecho = entity.DiasDerecho,
            DiasTomados = entity.DiasTomados,
            DiasPagados = entity.DiasPagados,
            DiasAjustados = entity.DiasAjustados,
            DiasVencidos = entity.DiasVencidos,
            Saldo = entity.Saldo,
            PrimaPagada = entity.PrimaPagada,
            FechaPagoPrima = entity.FechaPagoPrima,
            Comentario = entity.Comentario,
            Estatus = entity.Estatus,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc
        };
    }

    private static VacacionMovimientoDto ToMovimientoDto(VacacionMovimiento entity)
    {
        return new VacacionMovimientoDto
        {
            Id = entity.Id,
            EmpleadoId = entity.EmpleadoId,
            VacacionPeriodoId = entity.VacacionPeriodoId,
            CicloLaboral = entity.CicloLaboral != default
                ? entity.CicloLaboral
                : entity.VacacionPeriodo?.CicloLaboral ?? 1,
            AnioServicio = entity.VacacionPeriodo?.AnioServicio ?? 0,
            TipoMovimiento = entity.TipoMovimiento,
            FechaMovimiento = entity.FechaMovimiento,
            FechaInicioDisfrute = entity.FechaInicioDisfrute,
            FechaFinDisfrute = entity.FechaFinDisfrute,
            Dias = entity.Dias,
            SaldoAntes = entity.SaldoAntes,
            SaldoDespues = entity.SaldoDespues,
            Referencia = entity.Referencia,
            Comentario = entity.Comentario,
            UsuarioResponsableId = entity.UsuarioResponsableId,
            Origen = entity.Origen,
            ImportacionArchivo = entity.ImportacionArchivo,
            ImportacionHoja = entity.ImportacionHoja,
            ImportacionFila = entity.ImportacionFila,
            CreatedAtUtc = entity.CreatedAtUtc
        };
    }

    private sealed record VacacionCicloLaboralInfo(
        int CicloLaboral,
        DateOnly FechaBase);
}

