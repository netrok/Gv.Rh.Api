using Gv.Rh.Application.DTOs.Cumpleanios;
using Gv.Rh.Application.Interfaces;
using Gv.Rh.Domain.Common;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Gv.Rh.Infrastructure.Services;

public sealed class CumpleaniosService : ICumpleaniosService
{
    private readonly RhDbContext _dbContext;

    public CumpleaniosService(RhDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<CumpleaniosItemDto>> GetHoyAsync(
        int? sucursalId,
        int? departamentoId,
        CancellationToken cancellationToken = default)
    {
        var items = await BuildItemsAsync(sucursalId, departamentoId, cancellationToken);

        return items
            .Where(x => x.EsHoy)
            .OrderBy(x => x.NombreCompleto)
            .ToList();
    }

    public async Task<IReadOnlyList<CumpleaniosItemDto>> GetProximosAsync(
        int dias,
        int? sucursalId,
        int? departamentoId,
        CancellationToken cancellationToken = default)
    {
        if (dias < 1)
        {
            dias = 1;
        }

        if (dias > 90)
        {
            dias = 90;
        }

        var items = await BuildItemsAsync(sucursalId, departamentoId, cancellationToken);

        return items
            .Where(x => x.DiasRestantes >= 0 && x.DiasRestantes <= dias)
            .OrderBy(x => x.DiasRestantes)
            .ThenBy(x => x.NombreCompleto)
            .ToList();
    }

    public async Task<IReadOnlyList<CumpleaniosItemDto>> GetMesAsync(
        int anio,
        int mes,
        int? sucursalId,
        int? departamentoId,
        CancellationToken cancellationToken = default)
    {
        if (mes < 1 || mes > 12)
        {
            return Array.Empty<CumpleaniosItemDto>();
        }

        var items = await BuildItemsAsync(sucursalId, departamentoId, cancellationToken);

        return items
            .Where(x => x.Mes == mes)
            .OrderBy(x => x.Dia)
            .ThenBy(x => x.NombreCompleto)
            .ToList();
    }

    public async Task<CumpleaniosResumenDto> GetResumenAsync(
        int? sucursalId,
        int? departamentoId,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var items = await BuildItemsAsync(sucursalId, departamentoId, cancellationToken);

        return new CumpleaniosResumenDto
        {
            Hoy = items.Count(x => x.EsHoy),
            Proximos7Dias = items.Count(x => x.DiasRestantes >= 0 && x.DiasRestantes <= 7),
            EsteMes = items.Count(x => x.Mes == today.Month)
        };
    }

    private async Task<List<CumpleaniosItemDto>> BuildItemsAsync(
        int? sucursalId,
        int? departamentoId,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var query = _dbContext.Empleados
            .AsNoTracking()
            .Where(e =>
                e.Activo &&
                e.FechaNacimiento != null &&
                e.EstatusLaboralActual != EstatusLaboralEmpleado.BAJA);

        if (sucursalId.HasValue)
        {
            query = query.Where(e => e.SucursalId == sucursalId.Value);
        }

        if (departamentoId.HasValue)
        {
            query = query.Where(e => e.DepartamentoId == departamentoId.Value);
        }

        var empleados = await query
            .Select(e => new
            {
                e.Id,
                e.NumEmpleado,
                e.Nombres,
                e.ApellidoPaterno,
                e.ApellidoMaterno,
                e.FechaNacimiento,
                SucursalNombre = e.Sucursal != null ? e.Sucursal.Nombre : null,
                DepartamentoNombre = e.Departamento != null ? e.Departamento.Nombre : null,
                PuestoNombre = e.Puesto != null ? e.Puesto.Nombre : null,
                e.FotoRutaRelativa
            })
            .ToListAsync(cancellationToken);

        var items = empleados
            .Select(e =>
            {
                var fechaNacimiento = e.FechaNacimiento!.Value;
                var nextBirthday = GetNextBirthday(fechaNacimiento, today);
                var diasRestantes = GetDaysRemaining(today, nextBirthday);

                return new CumpleaniosItemDto
                {
                    EmpleadoId = e.Id,
                    NumEmpleado = e.NumEmpleado,
                    NombreCompleto = BuildNombreCompleto(
                        e.Nombres,
                        e.ApellidoPaterno,
                        e.ApellidoMaterno),
                    FechaNacimiento = fechaNacimiento,
                    Dia = fechaNacimiento.Day,
                    Mes = fechaNacimiento.Month,
                    EdadQueCumple = GetTurningAge(fechaNacimiento, nextBirthday),
                    SucursalNombre = e.SucursalNombre,
                    DepartamentoNombre = e.DepartamentoNombre,
                    PuestoNombre = e.PuestoNombre,
                    FotoUrl = BuildFotoUrl(e.FotoRutaRelativa),
                    EsHoy = diasRestantes == 0,
                    DiasRestantes = diasRestantes
                };
            })
            .OrderBy(x => x.DiasRestantes)
            .ThenBy(x => x.NombreCompleto)
            .ToList();

        return items;
    }

    private static string BuildNombreCompleto(
        string? nombres,
        string? apellidoPaterno,
        string? apellidoMaterno)
    {
        return string.Join(
            " ",
            new[] { nombres, apellidoPaterno, apellidoMaterno }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim()));
    }

    private static string? BuildFotoUrl(string? fotoRutaRelativa)
    {
        if (string.IsNullOrWhiteSpace(fotoRutaRelativa))
        {
            return null;
        }

        var normalized = fotoRutaRelativa.Replace("\\", "/").Trim();

        if (normalized.StartsWith("/"))
        {
            return normalized;
        }

        return "/" + normalized;
    }

    private static DateOnly GetNextBirthday(DateOnly birthDate, DateOnly today)
    {
        var year = today.Year;
        var month = birthDate.Month;
        var day = birthDate.Day;

        if (month == 2 && day == 29 && !DateTime.IsLeapYear(year))
        {
            day = 28;
        }

        var nextBirthday = new DateOnly(year, month, day);

        if (nextBirthday < today)
        {
            year++;

            month = birthDate.Month;
            day = birthDate.Day;

            if (month == 2 && day == 29 && !DateTime.IsLeapYear(year))
            {
                day = 28;
            }

            nextBirthday = new DateOnly(year, month, day);
        }

        return nextBirthday;
    }

    private static int GetTurningAge(DateOnly birthDate, DateOnly nextBirthday)
    {
        return nextBirthday.Year - birthDate.Year;
    }

    private static int GetDaysRemaining(DateOnly today, DateOnly nextBirthday)
    {
        return nextBirthday.DayNumber - today.DayNumber;
    }
}