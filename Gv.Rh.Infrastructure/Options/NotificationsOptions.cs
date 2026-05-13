namespace Gv.Rh.Infrastructure.Options;

public sealed class NotificationsOptions
{
    public const string SectionName = "Notifications";

    public bool Enabled { get; set; } = true;

    public int DefaultDiasAnticipacion { get; set; } = 30;

    public bool SchedulerEnabled { get; set; } = false;

    public TimeOnly SchedulerTimeLocal { get; set; } = new(7, 0);

    public bool AprobacionesSchedulerEnabled { get; set; } = false;

    public TimeOnly AprobacionesSchedulerTimeLocal { get; set; } = new(8, 0);

    public List<string> ExpedienteRecipients { get; set; } = [];

    public List<string> VacacionesRecipients { get; set; } = [];

    public bool OutboxEnabled { get; set; } = true;

    public int OutboxPollingSeconds { get; set; } = 60;

    public int OutboxBatchSize { get; set; } = 20;

    public int OutboxMaxAttempts { get; set; } = 5;

    public int OutboxRetryBaseMinutes { get; set; } = 5;
}
