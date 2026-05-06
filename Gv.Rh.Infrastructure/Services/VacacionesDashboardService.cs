using Gv.Rh.Application.DTOs.Vacaciones.Dashboard;
using Gv.Rh.Application.Interfaces;
using Gv.Rh.Domain.Common;
using Gv.Rh.Domain.Entities;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Gv.Rh.Infrastructure.Services;

public sealed class VacacionesDashboardService : IVacacionesDashboardService
{
    private readonly RhDbContext _db;

    public VacacionesDashboardService(RhDbContext db)
    {
        _db = db;
    }

    public async Task<VacacionesDashboardDto> GetDashboardAsync(
        CancellationToken cancellationToken = default)
    {
        var fechaCorte = DateOnly.FromDateTime(DateTime.Today);
        var fechaLimite30 = fechaCorte.AddDays(30);
        var inicioMes = new DateOnly(fechaCorte.Year, fechaCorte.Month, 1);

        var empleadosActivos = await _db.Empleados
            .AsNoTracking()
            .CountAsync(
                x => x.Activo &&
                     x.EstatusLaboralActual == EstatusLaboralEmpleado.ACTIVO,
                cancellationToken);

        var periodos = await _db.VacacionPeriodos
            .AsNoTracking()
            .Include(x => x.Empleado)
                .ThenInclude(x => x!.Sucursal)
            .Include(x => x.Empleado)
                .ThenInclude(x => x!.Departamento)
            .Include(x => x.Empleado)
                .ThenInclude(x => x!.Puesto)
            .Where(x =>
                x.Empleado != null &&
                x.Empleado.Activo &&
                x.Empleado.EstatusLaboralActual == EstatusLaboralEmpleado.ACTIVO)
            .ToListAsync(cancellationToken);

        var periodosAbiertos = periodos
            .Where(x => x.Estatus == EstatusVacacionPeriodo.ABIERTO)
            .ToList();

        var empleadosConPeriodoAbierto = periodosAbiertos
            .Select(x => x.EmpleadoId)
            .Distinct()
            .Count();

        var empleadosSinPeriodoAbierto = Math.Max(
            0,
            empleadosActivos - empleadosConPeriodoAbierto);

        var periodosConSaldo = periodosAbiertos
            .Where(x => x.Saldo > 0)
            .ToList();

        var empleadosConSaldo = periodosConSaldo
            .Select(x => x.EmpleadoId)
            .Distinct()
            .Count();

        var periodosVencidos = periodosConSaldo
            .Count(x => x.FechaLimiteDisfrute < fechaCorte);

        var periodosPorVencer30Dias = periodosConSaldo
            .Count(x =>
                x.FechaLimiteDisfrute >= fechaCorte &&
                x.FechaLimiteDisfrute <= fechaLimite30);

        var movimientosMes = await _db.VacacionMovimientos
            .AsNoTracking()
            .CountAsync(
                x => x.FechaMovimiento >= inicioMes &&
                     x.FechaMovimiento <= fechaCorte,
                cancellationToken);

        var movimientosImportacionLegacy = await _db.VacacionMovimientos
            .AsNoTracking()
            .CountAsync(
                x => x.Origen == "EXCEL_LEGACY" ||
                     x.ImportacionArchivo != null,
                cancellationToken);

        var topSaldos = periodosConSaldo
            .GroupBy(x => x.EmpleadoId)
            .Select(group =>
            {
                var first = group
                    .OrderByDescending(x => x.CicloLaboral)
                    .ThenByDescending(x => x.AnioServicio)
                    .First();

                var empleado = first.Empleado!;

                return new VacacionesDashboardSaldoAltoDto
                {
                    EmpleadoId = empleado.Id,
                    NumEmpleado = empleado.NumEmpleado,
                    NombreEmpleado = BuildNombreEmpleado(empleado),
                    Sucursal = empleado.Sucursal?.Nombre,
                    Departamento = empleado.Departamento?.Nombre,
                    Puesto = empleado.Puesto?.Nombre,
                    CicloLaboral = first.CicloLaboral,
                    PeriodosAbiertos = group.Count(),
                    Saldo = group.Sum(x => x.Saldo)
                };
            })
            .OrderByDescending(x => x.Saldo)
            .ThenBy(x => x.NombreEmpleado)
            .Take(10)
            .ToList();

        var periodosPorVencer = periodosConSaldo
            .Where(x =>
                x.FechaLimiteDisfrute >= fechaCorte &&
                x.FechaLimiteDisfrute <= fechaLimite30)
            .OrderBy(x => x.FechaLimiteDisfrute)
            .ThenBy(x => x.Empleado!.NumEmpleado)
            .Take(10)
            .Select(x =>
            {
                var empleado = x.Empleado!;

                return new VacacionesDashboardPeriodoVencerDto
                {
                    VacacionPeriodoId = x.Id,
                    EmpleadoId = empleado.Id,
                    NumEmpleado = empleado.NumEmpleado,
                    NombreEmpleado = BuildNombreEmpleado(empleado),
                    Sucursal = empleado.Sucursal?.Nombre,
                    Departamento = empleado.Departamento?.Nombre,
                    Puesto = empleado.Puesto?.Nombre,
                    CicloLaboral = x.CicloLaboral,
                    AnioServicio = x.AnioServicio,
                    FechaInicio = x.FechaInicio,
                    FechaFin = x.FechaFin,
                    FechaLimiteDisfrute = x.FechaLimiteDisfrute,
                    DiasParaVencer = x.FechaLimiteDisfrute.DayNumber - fechaCorte.DayNumber,
                    Saldo = x.Saldo
                };
            })
            .ToList();

        var ultimosMovimientos = await _db.VacacionMovimientos
            .AsNoTracking()
            .Include(x => x.Empleado)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.FechaMovimiento)
            .Take(10)
            .Select(x => new VacacionesDashboardMovimientoDto
            {
                MovimientoId = x.Id,
                EmpleadoId = x.EmpleadoId,
                NumEmpleado = x.Empleado != null ? x.Empleado.NumEmpleado : string.Empty,
                NombreEmpleado = x.Empleado != null
                    ? ((x.Empleado.Nombres ?? string.Empty) + " " +
                       (x.Empleado.ApellidoPaterno ?? string.Empty) + " " +
                       (x.Empleado.ApellidoMaterno ?? string.Empty)).Trim()
                    : string.Empty,
                VacacionPeriodoId = x.VacacionPeriodoId,
                CicloLaboral = x.CicloLaboral,
                TipoMovimiento = x.TipoMovimiento.ToString(),
                FechaMovimiento = x.FechaMovimiento,
                Dias = x.Dias,
                SaldoAntes = x.SaldoAntes,
                SaldoDespues = x.SaldoDespues,
                Referencia = x.Referencia,
                Origen = x.Origen,
                Comentario = x.Comentario,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var empleadosParaAniversario = await _db.Empleados
            .AsNoTracking()
            .Include(x => x.Sucursal)
            .Include(x => x.Departamento)
            .Include(x => x.Puesto)
            .Where(x =>
                x.Activo &&
                x.EstatusLaboralActual == EstatusLaboralEmpleado.ACTIVO)
            .ToListAsync(cancellationToken);

        var proximosAniversarios = empleadosParaAniversario
            .Select(empleado =>
            {
                var fechaBase = empleado.FechaReingresoActual ?? empleado.FechaIngreso;
                var proximoAniversario = CalcularProximoAniversario(fechaBase, fechaCorte);

                return new VacacionesDashboardAniversarioDto
                {
                    EmpleadoId = empleado.Id,
                    NumEmpleado = empleado.NumEmpleado,
                    NombreEmpleado = BuildNombreEmpleado(empleado),
                    Sucursal = empleado.Sucursal?.Nombre,
                    Departamento = empleado.Departamento?.Nombre,
                    Puesto = empleado.Puesto?.Nombre,
                    FechaBaseCicloLaboral = fechaBase,
                    ProximoAniversario = proximoAniversario,
                    DiasRestantes = proximoAniversario.DayNumber - fechaCorte.DayNumber,
                    AniosServicioCumplidos = CalcularAniosServicioCumplidos(fechaBase, fechaCorte)
                };
            })
            .Where(x => x.DiasRestantes >= 0 && x.DiasRestantes <= 30)
            .OrderBy(x => x.DiasRestantes)
            .ThenBy(x => x.NombreEmpleado)
            .Take(10)
            .ToList();

        return new VacacionesDashboardDto
        {
            FechaCorte = fechaCorte,

            EmpleadosActivos = empleadosActivos,
            EmpleadosConSaldo = empleadosConSaldo,
            EmpleadosSinPeriodoAbierto = empleadosSinPeriodoAbierto,

            PeriodosAbiertos = periodosAbiertos.Count,
            PeriodosVencidos = periodosVencidos,
            PeriodosPorVencer30Dias = periodosPorVencer30Dias,

            SaldoTotal = periodosConSaldo.Sum(x => x.Saldo),
            DiasDerechoTotal = periodos.Sum(x => x.DiasDerecho),
            DiasTomadosTotal = periodos.Sum(x => x.DiasTomados),
            DiasVencidosTotal = periodos.Sum(x => x.DiasVencidos),

            MovimientosMes = movimientosMes,
            MovimientosImportacionLegacy = movimientosImportacionLegacy,

            TopSaldos = topSaldos,
            PeriodosPorVencer = periodosPorVencer,
            UltimosMovimientos = ultimosMovimientos,
            ProximosAniversarios = proximosAniversarios
        };
    }

    private static int CalcularAniosServicioCumplidos(
        DateOnly fechaIngreso,
        DateOnly fechaReferencia)
    {
        var years = fechaReferencia.Year - fechaIngreso.Year;
        var anniversary = fechaIngreso.AddYears(years);

        if (anniversary > fechaReferencia)
            years--;

        return Math.Max(0, years);
    }

    private static DateOnly CalcularProximoAniversario(
        DateOnly fechaIngreso,
        DateOnly fechaReferencia)
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
}
