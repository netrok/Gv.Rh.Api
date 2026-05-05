namespace Gv.Rh.Application.DTOs.Vacaciones.Reports;

public sealed class VacacionesKardexReporteRowDto
{
    public int MovimientoId { get; set; }

    public int EmpleadoId { get; set; }
    public string NumEmpleado { get; set; } = string.Empty;
    public string NombreEmpleado { get; set; } = string.Empty;

    public string? Sucursal { get; set; }
    public string? Departamento { get; set; }
    public string? Puesto { get; set; }

    public string EstatusLaboral { get; set; } = string.Empty;
    public bool Activo { get; set; }

    public int VacacionPeriodoId { get; set; }

    public int CicloLaboral { get; set; }

    public int AnioServicio { get; set; }

    public DateOnly PeriodoFechaInicio { get; set; }
    public DateOnly PeriodoFechaFin { get; set; }

    public string TipoMovimiento { get; set; } = string.Empty;

    public DateOnly FechaMovimiento { get; set; }

    public DateOnly? FechaInicioDisfrute { get; set; }
    public DateOnly? FechaFinDisfrute { get; set; }

    public decimal Dias { get; set; }
    public decimal SaldoAntes { get; set; }
    public decimal SaldoDespues { get; set; }

    public string? Referencia { get; set; }
    public string? Comentario { get; set; }

    public int? UsuarioResponsableId { get; set; }
    public string? UsuarioResponsable { get; set; }

    public string? Origen { get; set; }

    public string? ImportacionArchivo { get; set; }
    public string? ImportacionHoja { get; set; }
    public int? ImportacionFila { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
