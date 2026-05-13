using Gv.Rh.Application.Interfaces;
using Gv.Rh.Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gv.Rh.Infrastructure.Services;

public sealed class AprobacionesNotificationHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly NotificationsOptions _options;
    private readonly ILogger<AprobacionesNotificationHostedService> _logger;

    public AprobacionesNotificationHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<NotificationsOptions> options,
        ILogger<AprobacionesNotificationHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Notificaciones deshabilitadas. Scheduler de aprobaciones no inicia.");
            return;
        }

        if (!_options.AprobacionesSchedulerEnabled)
        {
            _logger.LogInformation("Scheduler de aprobaciones deshabilitado.");
            return;
        }

        _logger.LogInformation(
            "Scheduler de aprobaciones iniciado. HoraLocal={HoraLocal}",
            _options.AprobacionesSchedulerTimeLocal);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.Now;
                var nextRun = GetNextRun(now, _options.AprobacionesSchedulerTimeLocal);
                var delay = nextRun - now;

                if (delay < TimeSpan.Zero)
                    delay = TimeSpan.FromMinutes(1);

                _logger.LogInformation(
                    "Próxima ejecución de aprobaciones: {NextRun}",
                    nextRun);

                await Task.Delay(delay, stoppingToken);

                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider
                    .GetRequiredService<IAprobacionesNotificationService>();

                var creadas = await service.NotificarResumenPendientesAsync(stoppingToken);

                _logger.LogInformation(
                    "Ejecución automática de aprobaciones completada. NotificacionesCreadas={Creadas}",
                    creadas);
            }
            catch (TaskCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ejecutando scheduler de aprobaciones.");

                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }
    }

    private static DateTime GetNextRun(DateTime now, TimeOnly timeLocal)
    {
        var todayRun = now.Date.Add(timeLocal.ToTimeSpan());

        return todayRun > now
            ? todayRun
            : todayRun.AddDays(1);
    }
}
