using Gv.Rh.Domain.Common;

namespace Gv.Rh.Domain.Entities;

public class Vacante
{
    public int Id { get; set; }
    public string Folio { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;

    public int DepartamentoId { get; set; }
    public Departamento Departamento { get; set; } = default!;

    public int PuestoId { get; set; }
    public Puesto Puesto { get; set; } = default!;

    public int SucursalId { get; set; }
    public Sucursal Sucursal { get; set; } = default!;

    public int NumeroPosiciones { get; set; } = 1;
    public string? Descripcion { get; set; }
    public string? Perfil { get; set; }

    public decimal? SalarioMinimo { get; set; }
    public decimal? SalarioMaximo { get; set; }

    public DateOnly FechaApertura { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public DateOnly? FechaCierre { get; set; }

    public EstatusVacante Estatus { get; set; } = EstatusVacante.BORRADOR;
    public bool Activo { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<Postulacion> Postulaciones { get; set; } = new List<Postulacion>();
}