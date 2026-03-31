using Gv.Rh.Domain.Common;

namespace Gv.Rh.Application.DTOs.Reclutamiento.Vacantes;

public sealed class VacanteListItemDto
{
    public int Id { get; set; }
    public string Folio { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;

    public int DepartamentoId { get; set; }
    public string DepartamentoNombre { get; set; } = string.Empty;

    public int PuestoId { get; set; }
    public string PuestoNombre { get; set; } = string.Empty;

    public int SucursalId { get; set; }
    public string SucursalNombre { get; set; } = string.Empty;

    public int NumeroPosiciones { get; set; }
    public int PosicionesCubiertas { get; set; }

    public DateOnly FechaApertura { get; set; }
    public DateOnly? FechaCierre { get; set; }

    public EstatusVacante Estatus { get; set; }
    public bool Activo { get; set; }
}