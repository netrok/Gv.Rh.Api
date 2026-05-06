namespace Gv.Rh.Application.Interfaces;

public interface INotificationOutboxService
{
    Task<long?> QueueEmailAsync(
        IEnumerable<string> to,
        string subject,
        string htmlBody,
        string tipo,
        string? correlationType = null,
        int? correlationId = null,
        CancellationToken cancellationToken = default);

    Task<int> ProcessPendingAsync(
        int batchSize,
        CancellationToken cancellationToken = default);
}