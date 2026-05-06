using Gv.Rh.Application.Interfaces;
using Gv.Rh.Domain.Common;
using Gv.Rh.Domain.Entities;
using Gv.Rh.Infrastructure.Options;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Gv.Rh.Infrastructure.Services;

public sealed class NotificationOutboxService : INotificationOutboxService
{
    private readonly RhDbContext _db;
    private readonly IEmailService _emailService;
    private readonly NotificationsOptions _options;
    private readonly ILogger<NotificationOutboxService> _logger;

    public NotificationOutboxService(
        RhDbContext db,
        IEmailService emailService,
        IOptions<NotificationsOptions> options,
        ILogger<NotificationOutboxService> logger)
    {
        _db = db;
        _emailService = emailService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<long?> QueueEmailAsync(
        IEnumerable<string> to,
        string subject,
        string htmlBody,
        string tipo,
        string? correlationType = null,
        int? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var recipients = NormalizeRecipients(to);

        if (recipients.Count == 0)
        {
            _logger.LogWarning(
                "No se encoló notificación {Tipo}. No hay destinatarios válidos. Correlation={CorrelationType}:{CorrelationId}",
                tipo,
                correlationType,
                correlationId);

            return null;
        }

        var now = DateTime.UtcNow;

        var entity = new NotificacionOutbox
        {
            Tipo = string.IsNullOrWhiteSpace(tipo) ? "GENERAL" : tipo.Trim(),
            Canal = "EMAIL",
            DestinatariosJson = JsonSerializer.Serialize(recipients),
            Subject = subject.Trim(),
            HtmlBody = htmlBody,
            Estado = EstatusNotificacionOutbox.PENDIENTE,
            Intentos = 0,
            MaxIntentos = Math.Max(1, _options.OutboxMaxAttempts),
            NextAttemptUtc = now,
            CorrelationType = string.IsNullOrWhiteSpace(correlationType) ? null : correlationType.Trim(),
            CorrelationId = correlationId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.NotificacionesOutbox.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Notificación {Tipo} encolada en outbox. Id={OutboxId}. Destinatarios={Destinatarios}",
            entity.Tipo,
            entity.Id,
            string.Join(", ", recipients));

        return entity.Id;
    }

    public async Task<int> ProcessPendingAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var staleLimit = now.AddMinutes(-15);
        var take = Math.Clamp(batchSize, 1, 100);

        var items = await _db.NotificacionesOutbox
            .Where(x =>
                x.Canal == "EMAIL" &&
                x.Intentos < x.MaxIntentos &&
                (
                    (x.Estado == EstatusNotificacionOutbox.PENDIENTE &&
                     (!x.NextAttemptUtc.HasValue || x.NextAttemptUtc <= now)) ||
                    (x.Estado == EstatusNotificacionOutbox.FALLIDA &&
                     (!x.NextAttemptUtc.HasValue || x.NextAttemptUtc <= now)) ||
                    (x.Estado == EstatusNotificacionOutbox.ENVIANDO &&
                     x.UpdatedAtUtc <= staleLimit)
                ))
            .OrderBy(x => x.NextAttemptUtc ?? x.CreatedAtUtc)
            .ThenBy(x => x.Id)
            .Take(take)
            .ToListAsync(cancellationToken);

        var processed = 0;

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            item.Estado = EstatusNotificacionOutbox.ENVIANDO;
            item.Intentos += 1;
            item.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            try
            {
                var recipients = DeserializeRecipients(item.DestinatariosJson);

                if (recipients.Count == 0)
                    throw new InvalidOperationException("La notificación no tiene destinatarios válidos.");

                await _emailService.SendAsync(
                    recipients,
                    item.Subject,
                    item.HtmlBody,
                    cancellationToken);

                item.Estado = EstatusNotificacionOutbox.ENVIADA;
                item.SentAtUtc = DateTime.UtcNow;
                item.UltimoError = null;
                item.UpdatedAtUtc = DateTime.UtcNow;

                processed++;

                _logger.LogInformation(
                    "Notificación outbox enviada. Id={OutboxId}. Tipo={Tipo}. Intentos={Intentos}",
                    item.Id,
                    item.Tipo,
                    item.Intentos);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                item.Estado = EstatusNotificacionOutbox.FALLIDA;
                item.UltimoError = Truncate(ex.Message, 3900);
                item.NextAttemptUtc = BuildNextAttempt(item.Intentos);
                item.UpdatedAtUtc = DateTime.UtcNow;

                _logger.LogWarning(
                    ex,
                    "Falló envío de notificación outbox. Id={OutboxId}. Tipo={Tipo}. Intento={Intentos}/{MaxIntentos}. Próximo intento={NextAttemptUtc}",
                    item.Id,
                    item.Tipo,
                    item.Intentos,
                    item.MaxIntentos,
                    item.NextAttemptUtc);
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        return processed;
    }

    private DateTime BuildNextAttempt(int intentos)
    {
        var baseMinutes = Math.Max(1, _options.OutboxRetryBaseMinutes);
        var minutes = Math.Min(120, baseMinutes * Math.Pow(2, Math.Max(0, intentos - 1)));

        return DateTime.UtcNow.AddMinutes(minutes);
    }

    private static List<string> NormalizeRecipients(IEnumerable<string> recipients)
    {
        return recipients
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Where(x => x.Contains('@'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> DeserializeRecipients(string json)
    {
        try
        {
            return NormalizeRecipients(JsonSerializer.Deserialize<List<string>>(json) ?? []);
        }
        catch
        {
            return [];
        }
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Length <= maxLength
            ? value
            : value[..maxLength];
    }
}