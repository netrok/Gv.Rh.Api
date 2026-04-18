namespace Gv.Rh.Application.DTOs.Audit;

public sealed class AuditReportQueryDto
{
    public string? EntityName { get; set; }
    public string? RecordId { get; set; }
    public string? Action { get; set; }
    public string? Email { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? Q { get; set; }
}