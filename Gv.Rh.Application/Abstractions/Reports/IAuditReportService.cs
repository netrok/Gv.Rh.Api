using Gv.Rh.Application.DTOs.Audit;
using Gv.Rh.Application.DTOs.Common;

namespace Gv.Rh.Application.Abstractions.Reports;

public interface IAuditReportService
{
    Task<ReportFileDto> BuildXlsxAsync(AuditReportQueryDto query, CancellationToken cancellationToken);
    Task<ReportFileDto> BuildPdfAsync(AuditReportQueryDto query, CancellationToken cancellationToken);
}