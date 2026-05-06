using Gv.Rh.Domain.Common;

namespace Gv.Rh.Domain.Entities;

public class NotificacionOutbox
{
    public long Id { get; set; }

    public string Tipo { get; set; } = string.Empty;

    public string Canal { get; set; } = "EMAIL";

    public string DestinatariosJson { get; set; } = "[]";

    public string Subject { get; set; } = string.Empty;

    public string HtmlBody { get; set; } = string.Empty;

    public EstatusNotificacionOutbox Estado { get; set; } =
        EstatusNotificacionOutbox.PENDIENTE;

    public int Intentos { get; set; }

    public int MaxIntentos { get; set; } = 5;

    public string? UltimoError { get; set; }

    public DateTime? NextAttemptUtc { get; set; }

    public DateTime? SentAtUtc { get; set; }

    public string? CorrelationType { get; set; }

    public int? CorrelationId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}