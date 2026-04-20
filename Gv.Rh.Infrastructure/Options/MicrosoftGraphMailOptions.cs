namespace Gv.Rh.Infrastructure.Options;

public sealed class MicrosoftGraphMailOptions
{
    public const string SectionName = "MicrosoftGraphMail";

    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string SenderUserId { get; set; } = string.Empty;
}