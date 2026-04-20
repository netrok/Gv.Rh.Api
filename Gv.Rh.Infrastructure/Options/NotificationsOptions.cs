namespace Gv.Rh.Infrastructure.Options;

public sealed class NotificationsOptions
{
    public const string SectionName = "Notifications";

    public List<string> ExpedienteRecipients { get; set; } = [];
}