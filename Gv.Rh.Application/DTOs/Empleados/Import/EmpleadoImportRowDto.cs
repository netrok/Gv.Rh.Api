namespace Gv.Rh.Application.DTOs.Empleados.Import;

public sealed class EmpleadoImportRowDto
{
    public int RowNumber { get; set; }

    public string? NumEmpleado { get; set; }
    public string? Nombres { get; set; }
    public string? ApellidoPaterno { get; set; }
    public string? ApellidoMaterno { get; set; }
    public string? FechaNacimientoRaw { get; set; }
    public string? FechaIngresoRaw { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? ActivoRaw { get; set; }

    public string? Curp { get; set; }
    public string? Rfc { get; set; }
    public string? Nss { get; set; }
    public string? Sexo { get; set; }
    public string? EstadoCivil { get; set; }
    public string? Nacionalidad { get; set; }

    public string? Calle { get; set; }
    public string? NumeroExterior { get; set; }
    public string? NumeroInterior { get; set; }
    public string? Colonia { get; set; }
    public string? Municipio { get; set; }
    public string? Estado { get; set; }
    public string? CodigoPostal { get; set; }
    public string? Pais { get; set; }

    public string? ContactoEmergenciaNombre { get; set; }
    public string? ContactoEmergenciaTelefono { get; set; }
    public string? ContactoEmergenciaParentesco { get; set; }

    public string? Departamento { get; set; }
    public string? Puesto { get; set; }
    public string? Sucursal { get; set; }
}