using Gv.Rh.Domain.Common;

namespace Gv.Rh.Application.DTOs.Reclutamiento.Vacantes;

public sealed class VacanteDetailDto
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

    public string? Descripcion { get; set; }
    public string? Perfil { get; set; }

    public decimal? SalarioMinimo { get; set; }
    public decimal? SalarioMaximo { get; set; }

    public DateOnly FechaApertura { get; set; }
    public DateOnly? FechaCierre { get; set; }

    public EstatusVacante Estatus { get; set; }
    public bool Activo { get; set; }

    public int PostulacionesTotal { get; set; }
    public int ContratadosTotal { get; set; }
    public int DescartadosTotal { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}