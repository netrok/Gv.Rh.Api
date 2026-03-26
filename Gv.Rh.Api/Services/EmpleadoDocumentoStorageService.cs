using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Gv.Rh.Api.Services;

public interface IEmpleadoDocumentoStorageService
{
    Task<EmpleadoDocumentoStoredFile> SaveAsync(
        int empleadoId,
        IFormFile archivo,
        CancellationToken cancellationToken = default);

    Task<EmpleadoDocumentoStoredFile> ReplaceAsync(
        int empleadoId,
        IFormFile archivo,
        string? rutaRelativaAnterior,
        CancellationToken cancellationToken = default);

    Stream OpenRead(string rutaRelativa);

    bool DeleteIfExists(string? rutaRelativa);
}

public sealed class EmpleadoDocumentoStorageService : IEmpleadoDocumentoStorageService
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

    private static readonly Dictionary<string, string> ContentTypesByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp"
    };

    private readonly ILogger<EmpleadoDocumentoStorageService> _logger;
    private readonly string _basePath;

    public EmpleadoDocumentoStorageService(ILogger<EmpleadoDocumentoStorageService> logger)
    {
        _logger = logger;
        _basePath = Path.Combine(AppContext.BaseDirectory, "storage", "empleados-documentos");
        Directory.CreateDirectory(_basePath);
    }

    public async Task<EmpleadoDocumentoStoredFile> SaveAsync(
        int empleadoId,
        IFormFile archivo,
        CancellationToken cancellationToken = default)
    {
        if (empleadoId <= 0)
            throw new ArgumentOutOfRangeException(nameof(empleadoId), "El empleado es inválido.");

        ArgumentNullException.ThrowIfNull(archivo);

        if (archivo.Length <= 0)
            throw new InvalidOperationException("El archivo está vacío.");

        if (archivo.Length > MaxFileSizeBytes)
            throw new InvalidOperationException($"El archivo excede el tamaño máximo permitido de {MaxFileSizeBytes / (1024 * 1024)} MB.");

        var originalFileName = Path.GetFileName(archivo.FileName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(originalFileName))
            throw new InvalidOperationException("El archivo no tiene un nombre válido.");

        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
            throw new InvalidOperationException("Solo se permiten archivos PDF, JPG, JPEG, PNG o WEBP.");

        var contentType = ResolveContentType(extension, archivo.ContentType);

        var empleadoFolderPath = Path.Combine(_basePath, empleadoId.ToString());
        Directory.CreateDirectory(empleadoFolderPath);

        var safeBaseName = SanitizeFileNameWithoutExtension(Path.GetFileNameWithoutExtension(originalFileName));
        var guidFragment = Guid.NewGuid().ToString("N")[..8];
        var uniquePrefix = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{guidFragment}";
        var storedFileName = $"{uniquePrefix}_{safeBaseName}{extension}";
        var fullPath = Path.Combine(empleadoFolderPath, storedFileName);

        await using (var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await archivo.CopyToAsync(stream, cancellationToken);
        }

        var relativePath = Path.Combine("storage", "empleados-documentos", empleadoId.ToString(), storedFileName)
            .Replace("\\", "/");

        return new EmpleadoDocumentoStoredFile
        {
            NombreArchivoOriginal = originalFileName,
            NombreArchivoGuardado = storedFileName,
            RutaRelativa = relativePath,
            MimeType = contentType,
            TamanoBytes = archivo.Length
        };
    }

    public async Task<EmpleadoDocumentoStoredFile> ReplaceAsync(
        int empleadoId,
        IFormFile archivo,
        string? rutaRelativaAnterior,
        CancellationToken cancellationToken = default)
    {
        var stored = await SaveAsync(empleadoId, archivo, cancellationToken);

        if (!string.IsNullOrWhiteSpace(rutaRelativaAnterior))
        {
            DeleteIfExists(rutaRelativaAnterior);
        }

        return stored;
    }

    public Stream OpenRead(string rutaRelativa)
    {
        var fullPath = ResolveAndValidateFullPath(rutaRelativa);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException("No se encontró el archivo solicitado.", rutaRelativa);

        return new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public bool DeleteIfExists(string? rutaRelativa)
    {
        if (string.IsNullOrWhiteSpace(rutaRelativa))
            return false;

        var fullPath = ResolveAndValidateFullPath(rutaRelativa);

        if (!File.Exists(fullPath))
            return false;

        try
        {
            File.Delete(fullPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo eliminar el archivo físico: {Path}", fullPath);
            return false;
        }
    }

    private string ResolveAndValidateFullPath(string rutaRelativa)
    {
        if (string.IsNullOrWhiteSpace(rutaRelativa))
            throw new ArgumentException("La ruta relativa es obligatoria.", nameof(rutaRelativa));

        var normalizedRelativePath = rutaRelativa
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);

        var fullPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, normalizedRelativePath));
        var expectedBasePath = Path.GetFullPath(_basePath);

        if (!fullPath.StartsWith(expectedBasePath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("La ruta del archivo es inválida.");

        return fullPath;
    }

    private static string ResolveContentType(string extension, string? providedContentType)
    {
        if (!string.IsNullOrWhiteSpace(providedContentType) &&
            ContentTypesByExtension.TryGetValue(extension, out var expectedContentType) &&
            string.Equals(providedContentType, expectedContentType, StringComparison.OrdinalIgnoreCase))
        {
            return expectedContentType;
        }

        return ContentTypesByExtension[extension];
    }

    private static string SanitizeFileNameWithoutExtension(string fileNameWithoutExtension)
    {
        if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
            return "archivo";

        var invalidChars = Path.GetInvalidFileNameChars();
        var cleanChars = fileNameWithoutExtension
            .Trim()
            .Select(ch => invalidChars.Contains(ch) ? '_' : ch)
            .ToArray();

        var sanitized = new string(cleanChars);

        while (sanitized.Contains("  ", StringComparison.Ordinal))
            sanitized = sanitized.Replace("  ", " ", StringComparison.Ordinal);

        sanitized = sanitized
            .Replace(" ", "_", StringComparison.Ordinal)
            .Replace(".", "_", StringComparison.Ordinal);

        if (sanitized.Length > 60)
            sanitized = sanitized[..60];

        return string.IsNullOrWhiteSpace(sanitized) ? "archivo" : sanitized;
    }
}

public sealed class EmpleadoDocumentoStoredFile
{
    public string NombreArchivoOriginal { get; set; } = string.Empty;
    public string NombreArchivoGuardado { get; set; } = string.Empty;
    public string RutaRelativa { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long TamanoBytes { get; set; }
}