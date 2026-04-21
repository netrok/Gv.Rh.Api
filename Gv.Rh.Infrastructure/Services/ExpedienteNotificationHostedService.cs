using Gv.Rh.Application.Interfaces;
using Gv.Rh.Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gv.Rh.Infrastructure.Services;

public sealed class ExpedienteNotificationHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly NotificationsOptions _options;
    private readonly ILogger<ExpedienteNotificationHostedService> _logger;

    public ExpedienteNotificationHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<NotificationsOptions> options,
        ILogger<ExpedienteNotificationHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.SchedulerEnabled)
        {
            _logger.LogInformation("Scheduler de expediente deshabilitado.");
            return;
        }

        _logger.LogInformation("Scheduler de expediente iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.Now;
                var nextRun = now.Date.Add(_options.SchedulerTimeLocal.ToTimeSpan());

                if (nextRun <= now)
                {
                    nextRun = nextRun.AddDays(1);
                }

                var delay = nextRun - now;

                _logger.LogInformation("Próxima ejecución de expediente: {NextRun}", nextRun);

                await Task.Delay(delay, stoppingToken);

                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IExpedienteNotificationService>();

                await service.EnviarResumenAsync(
                    _options.DefaultDiasAnticipacion,
                    incluirVencidos: true,
                    cancellationToken: stoppingToken);

                _logger.LogInformation("Ejecución automática de expediente completada.");
            }
            catch (TaskCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ejecutando scheduler de expediente.");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }
}