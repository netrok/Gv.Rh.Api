using Gv.Rh.Domain.Common;

namespace Gv.Rh.Application.DTOs.Empleados;

public class EmpleadoDto
{
    public int Id { get; set; }

    public string NumEmpleado { get; set; } = string.Empty;

    public string Nombres { get; set; } = string.Empty;
    public string ApellidoPaterno { get; set; } = string.Empty;
    public string? ApellidoMaterno { get; set; }

    public DateOnly? FechaNacimiento { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }

    public DateOnly FechaIngreso { get; set; }
    public bool Activo { get; set; }

    public int? DepartamentoId { get; set; }
    public string? DepartamentoNombre { get; set; }

    public int? PuestoId { get; set; }
    public string? PuestoNombre { get; set; }

    public int? SucursalId { get; set; }
    public string? SucursalNombre { get; set; }

    public int? AprobadorPrimarioEmpleadoId { get; set; }
    public string? AprobadorPrimarioNombre { get; set; }

    public int? AprobadorSecundarioEmpleadoId { get; set; }
    public string? AprobadorSecundarioNombre { get; set; }

    // Identificación
    public string? Curp { get; set; }
    public string? Rfc { get; set; }
    public string? Nss { get; set; }

    // Personales
    public SexoEmpleado Sexo { get; set; }
    public EstadoCivilEmpleado EstadoCivil { get; set; }
    public string? Nacionalidad { get; set; }

    // Domicilio
    public string? DireccionCalle { get; set; }
    public string? DireccionNumeroExterior { get; set; }
    public string? DireccionNumeroInterior { get; set; }
    public string? DireccionColonia { get; set; }
    public string? DireccionCiudad { get; set; }
    public string? DireccionEstado { get; set; }
    public string? DireccionCodigoPostal { get; set; }

    // Fiscales
    public string? CodigoPostalFiscal { get; set; }
    public string? EntidadFiscal { get; set; }

    // Emergencia
    public string? ContactoEmergenciaNombre { get; set; }
    public string? ContactoEmergenciaTelefono { get; set; }
    public string? ContactoEmergenciaParentesco { get; set; }

    // Laboral
    public EstatusLaboralEmpleado EstatusLaboralActual { get; set; }
    public DateOnly? FechaBajaActual { get; set; }
    public TipoBajaEmpleado? TipoBajaActual { get; set; }
    public DateOnly? FechaReingresoActual { get; set; }
    public bool Recontratable { get; set; }

    // Foto
    public string? FotoUrl { get; set; }
    public bool TieneFoto { get; set; }
    public string? FotoNombreOriginal { get; set; }
    public string? FotoMimeType { get; set; }
    public long? FotoTamanoBytes { get; set; }

    // Auditoría / metadata
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}