namespace Gv.Rh.Infrastructure.Options;

public sealed class NotificationsOptions
{
    public const string SectionName = "Notifications";

    public bool Enabled { get; set; } = true;

    public int DefaultDiasAnticipacion { get; set; } = 30;

    public bool SchedulerEnabled { get; set; } = false;

    public TimeOnly SchedulerTimeLocal { get; set; } = new(7, 0);

    public List<string> ExpedienteRecipients { get; set; } = [];
}