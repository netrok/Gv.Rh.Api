using Gv.Rh.Domain.Common;
using System.ComponentModel.DataAnnotations;

namespace Gv.Rh.Application.DTOs.Empleados;

public class EmpleadoCreateDto
{
    [Required]
    [MaxLength(30)]
    public string NumEmpleado { get; set; } = string.Empty;

    [Required]
    [StringLength(120, MinimumLength = 2)]
    public string Nombres { get; set; } = string.Empty;

    [Required]
    [StringLength(120, MinimumLength = 2)]
    public string ApellidoPaterno { get; set; } = string.Empty;

    [StringLength(120)]
    public string? ApellidoMaterno { get; set; }

    public DateOnly? FechaNacimiento { get; set; }

    [StringLength(15)]
    [RegularExpression(@"^\d{10,15}$", ErrorMessage = "El teléfono debe contener entre 10 y 15 dígitos.")]
    public string? Telefono { get; set; }

    [EmailAddress]
    [StringLength(160)]
    public string? Email { get; set; }

    [Required]
    public DateOnly FechaIngreso { get; set; }

    public bool Activo { get; set; } = true;

    public int? DepartamentoId { get; set; }
    public int? PuestoId { get; set; }
    public int? SucursalId { get; set; }

    // Identificación
    [StringLength(18)]
    [RegularExpression(
        @"^[A-Z]{4}\d{6}[HM][A-Z]{5}[A-Z0-9]\d$",
        ErrorMessage = "La CURP no tiene un formato válido.")]
    public string? Curp { get; set; }

    [StringLength(13)]
    [RegularExpression(
        @"^([A-Z&Ñ]{3,4})\d{6}([A-Z\d]{3})$",
        ErrorMessage = "El RFC no tiene un formato válido.")]
    public string? Rfc { get; set; }

    [StringLength(11)]
    [RegularExpression(
        @"^\d{11}$",
        ErrorMessage = "El NSS debe contener exactamente 11 dígitos.")]
    public string? Nss { get; set; }

    // Personales
    public SexoEmpleado Sexo { get; set; } = SexoEmpleado.NoEspecificado;

    public EstadoCivilEmpleado EstadoCivil { get; set; } = EstadoCivilEmpleado.NoEspecificado;

    [StringLength(80)]
    public string? Nacionalidad { get; set; }

    // Domicilio
    [StringLength(150)]
    public string? DireccionCalle { get; set; }

    [StringLength(20)]
    public string? DireccionNumeroExterior { get; set; }

    [StringLength(20)]
    public string? DireccionNumeroInterior { get; set; }

    [StringLength(120)]
    public string? DireccionColonia { get; set; }

    [StringLength(120)]
    public string? DireccionCiudad { get; set; }

    [StringLength(120)]
    public string? DireccionEstado { get; set; }

    [StringLength(5)]
    [RegularExpression(
        @"^\d{5}$",
        ErrorMessage = "El código postal debe contener exactamente 5 dígitos.")]
    public string? DireccionCodigoPostal { get; set; }

    // Fiscales
    [StringLength(5)]
    [RegularExpression(
        @"^\d{5}$",
        ErrorMessage = "El código postal fiscal debe contener exactamente 5 dígitos.")]
    public string? CodigoPostalFiscal { get; set; }

    [StringLength(120)]
    public string? EntidadFiscal { get; set; }

    // Emergencia
    [StringLength(150)]
    public string? ContactoEmergenciaNombre { get; set; }

    [StringLength(15)]
    [RegularExpression(
        @"^\d{10,15}$",
        ErrorMessage = "El teléfono de emergencia debe contener entre 10 y 15 dígitos.")]
    public string? ContactoEmergenciaTelefono { get; set; }

    [StringLength(60)]
    public string? ContactoEmergenciaParentesco { get; set; }
}