using Gv.Rh.Application.Interfaces;
using Gv.Rh.Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gv.Rh.Infrastructure.Services;

public sealed class NotificationOutboxHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly NotificationsOptions _options;
    private readonly ILogger<NotificationOutboxHostedService> _logger;

    public NotificationOutboxHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<NotificationsOptions> options,
        ILogger<NotificationOutboxHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollingSeconds = Math.Max(10, _options.OutboxPollingSeconds);
        var delay = TimeSpan.FromSeconds(pollingSeconds);

        _logger.LogInformation(
            "NotificationOutboxHostedService iniciado. Enabled={Enabled}. OutboxEnabled={OutboxEnabled}. PollingSeconds={PollingSeconds}",
            _options.Enabled,
            _options.OutboxEnabled,
            pollingSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_options.Enabled && _options.OutboxEnabled)
                {
                    using var scope = _scopeFactory.CreateScope();

                    var service = scope.ServiceProvider
                        .GetRequiredService<INotificationOutboxService>();

                    var processed = await service.ProcessPendingAsync(
                        Math.Max(1, _options.OutboxBatchSize),
                        stoppingToken);

                    if (processed > 0)
                    {
                        _logger.LogInformation(
                            "Outbox procesó {Processed} notificación(es).",
                            processed);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando notificaciones outbox.");
            }

            await Task.Delay(delay, stoppingToken);
        }
    }
}