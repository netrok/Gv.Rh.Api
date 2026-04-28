using Gv.Rh.Domain.Common;
using System.ComponentModel.DataAnnotations;

namespace Gv.Rh.Domain.Entities;

public class Empleado
{
    public int Id { get; set; }

    [Required]
    [MaxLength(30)]
    public string NumEmpleado { get; set; } = string.Empty;

    [Required]
    [MaxLength(120)]
    public string Nombres { get; set; } = string.Empty;

    [Required]
    [MaxLength(120)]
    public string ApellidoPaterno { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? ApellidoMaterno { get; set; }

    public DateOnly? FechaNacimiento { get; set; }

    [MaxLength(15)]
    public string? Telefono { get; set; }

    [MaxLength(160)]
    [EmailAddress]
    public string? Email { get; set; }

    public DateOnly FechaIngreso { get; set; }

    public bool Activo { get; set; } = true;

    public EstatusLaboralEmpleado EstatusLaboralActual { get; set; } = EstatusLaboralEmpleado.ACTIVO;

    public DateOnly? FechaBajaActual { get; set; }

    public TipoBajaEmpleado? TipoBajaActual { get; set; }

    public DateOnly? FechaReingresoActual { get; set; }

    public bool Recontratable { get; set; } = true;

    public int? DepartamentoId { get; set; }
    public Departamento? Departamento { get; set; }

    public int? PuestoId { get; set; }
    public Puesto? Puesto { get; set; }

    public int? SucursalId { get; set; }
    public Sucursal? Sucursal { get; set; }

    // Aprobación jerárquica
    public int? AprobadorPrimarioEmpleadoId { get; set; }
    public Empleado? AprobadorPrimario { get; set; }

    public int? AprobadorSecundarioEmpleadoId { get; set; }
    public Empleado? AprobadorSecundario { get; set; }

    public ICollection<Empleado> SubordinadosComoAprobadorPrimario { get; set; } = new List<Empleado>();
    public ICollection<Empleado> SubordinadosComoAprobadorSecundario { get; set; } = new List<Empleado>();

    // Identificación
    [MaxLength(18)]
    public string? Curp { get; set; }

    [MaxLength(13)]
    public string? Rfc { get; set; }

    [MaxLength(11)]
    public string? Nss { get; set; }

    // Personales
    public SexoEmpleado Sexo { get; set; } = SexoEmpleado.NoEspecificado;

    public EstadoCivilEmpleado EstadoCivil { get; set; } = EstadoCivilEmpleado.NoEspecificado;

    [MaxLength(80)]
    public string? Nacionalidad { get; set; }

    // Domicilio
    [MaxLength(150)]
    public string? DireccionCalle { get; set; }

    [MaxLength(20)]
    public string? DireccionNumeroExterior { get; set; }

    [MaxLength(20)]
    public string? DireccionNumeroInterior { get; set; }

    [MaxLength(120)]
    public string? DireccionColonia { get; set; }

    [MaxLength(120)]
    public string? DireccionCiudad { get; set; }

    [MaxLength(120)]
    public string? DireccionEstado { get; set; }

    [MaxLength(5)]
    public string? DireccionCodigoPostal { get; set; }

    // Fiscales
    [MaxLength(5)]
    public string? CodigoPostalFiscal { get; set; }

    [MaxLength(120)]
    public string? EntidadFiscal { get; set; }

    // Emergencia
    [MaxLength(150)]
    public string? ContactoEmergenciaNombre { get; set; }

    [MaxLength(15)]
    public string? ContactoEmergenciaTelefono { get; set; }

    [MaxLength(60)]
    public string? ContactoEmergenciaParentesco { get; set; }

    // Foto
    [MaxLength(260)]
    public string? FotoNombreOriginal { get; set; }

    [MaxLength(260)]
    public string? FotoNombreGuardado { get; set; }

    [MaxLength(500)]
    public string? FotoRutaRelativa { get; set; }

    [MaxLength(100)]
    public string? FotoMimeType { get; set; }

    public long? FotoTamanoBytes { get; set; }

    public DateTime? FotoUpdatedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<EmpleadoDocumento> Documentos { get; set; } = new List<EmpleadoDocumento>();

    public ICollection<EmpleadoMovimientoLaboral> MovimientosLaborales { get; set; } = new List<EmpleadoMovimientoLaboral>();
}