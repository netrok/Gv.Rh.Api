using Gv.Rh.Application.Abstractions.Reports;
using Gv.Rh.Application.DTOs.Audit;
using Gv.Rh.Domain.Entities;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Gv.Rh.Api.Controllers;

[ApiController]
[Route("api/audit")]
[Authorize(Roles = "ADMIN,RRHH")]
public class AuditController : ControllerBase
{
    private readonly RhDbContext _db;
    private readonly IAuditReportService _auditReportService;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public AuditController(
        RhDbContext db,
        IAuditReportService auditReportService)
    {
        _db = db;
        _auditReportService = auditReportService;
    }

    // GET /api/audit?entityName=Empleado&recordId=123&action=UPDATE&email=rrhh@rh.local&from=2026-03-01&to=2026-03-04&q=algo&page=1&pageSize=50
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? entityName = null,
        [FromQuery] string? recordId = null,
        [FromQuery] string? action = null,
        [FromQuery] string? email = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? q = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = ApplyFilters(
                _db.AuditLogs.AsNoTracking(),
                entityName,
                recordId,
                action,
                email,
                from,
                to,
                q)
            .OrderByDescending(x => x.OccurredAtUtc)
            .ThenByDescending(x => x.Id);

        var total = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.OccurredAtUtc,
                x.Action,
                x.EntityName,
                x.RecordId,
                x.UserId,
                x.UserEmail,
                x.UserRole,
                x.IpAddress,
                x.UserAgent
            })
            .ToListAsync();

        return Ok(new
        {
            page,
            pageSize,
            total,
            totalPages = (int)Math.Ceiling(total / (double)pageSize),
            items
        });
    }

    // GET /api/audit/123?pretty=true
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, [FromQuery] bool pretty = false)
    {
        var item = await _db.AuditLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (item is null)
            return NotFound();

        var oldValuesJson = item.OldValuesJson;
        var newValuesJson = item.NewValuesJson;
        var changedColumnsJson = item.ChangedColumnsJson;

        if (pretty)
        {
            if (!string.IsNullOrWhiteSpace(oldValuesJson))
                oldValuesJson = PrettyJsonOrOriginal(oldValuesJson);

            if (!string.IsNullOrWhiteSpace(newValuesJson))
                newValuesJson = PrettyJsonOrOriginal(newValuesJson);

            if (!string.IsNullOrWhiteSpace(changedColumnsJson))
                changedColumnsJson = PrettyJsonOrOriginal(changedColumnsJson);
        }

        return Ok(new
        {
            item.Id,
            item.OccurredAtUtc,
            item.UserId,
            item.UserEmail,
            item.UserRole,
            item.Action,
            item.EntityName,
            item.RecordId,
            item.IpAddress,
            item.UserAgent,
            OldValuesJson = oldValuesJson,
            NewValuesJson = newValuesJson,
            ChangedColumnsJson = changedColumnsJson
        });
    }

    // GET /api/audit/export.xlsx?entityName=...&recordId=...&action=...&email=...&from=...&to=...&q=...
    [HttpGet("export.xlsx")]
    public async Task<IActionResult> ExportXlsx(
        [FromQuery] AuditReportQueryDto query,
        CancellationToken cancellationToken)
    {
        var report = await _auditReportService.BuildXlsxAsync(query, cancellationToken);
        return File(report.Content, report.ContentType, report.FileName);
    }

    // GET /api/audit/export.pdf?entityName=...&recordId=...&action=...&email=...&from=...&to=...&q=...
    [HttpGet("export.pdf")]
    public async Task<IActionResult> ExportPdf(
        [FromQuery] AuditReportQueryDto query,
        CancellationToken cancellationToken)
    {
        var report = await _auditReportService.BuildPdfAsync(query, cancellationToken);
        return File(report.Content, report.ContentType, report.FileName);
    }

    private static IQueryable<AuditLog> ApplyFilters(
        IQueryable<AuditLog> qset,
        string? entityName,
        string? recordId,
        string? action,
        string? email,
        DateTime? from,
        DateTime? to,
        string? q)
    {
        if (!string.IsNullOrWhiteSpace(entityName))
            qset = qset.Where(x => x.EntityName == entityName.Trim());

        if (!string.IsNullOrWhiteSpace(recordId))
            qset = qset.Where(x => x.RecordId == recordId.Trim());

        if (!string.IsNullOrWhiteSpace(action))
            qset = qset.Where(x => x.Action == action.Trim());

        if (!string.IsNullOrWhiteSpace(email))
        {
            var term = email.Trim();
            qset = qset.Where(x =>
                x.UserEmail != null &&
                EF.Functions.ILike(x.UserEmail, $"%{term}%"));
        }

        if (from.HasValue)
            qset = qset.Where(x => x.OccurredAtUtc >= ToUtcFrom(from.Value));

        if (to.HasValue)
            qset = qset.Where(x => x.OccurredAtUtc <= ToUtcToInclusive(to.Value));

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();

            qset = qset.Where(x =>
                (x.UserEmail != null && EF.Functions.ILike(x.UserEmail, $"%{term}%")) ||
                (x.UserRole != null && EF.Functions.ILike(x.UserRole, $"%{term}%")) ||
                EF.Functions.ILike(x.EntityName, $"%{term}%") ||
                EF.Functions.ILike(x.Action, $"%{term}%") ||
                EF.Functions.ILike(x.RecordId, $"%{term}%") ||
                (x.IpAddress != null && EF.Functions.ILike(x.IpAddress, $"%{term}%")) ||
                (x.OldValuesJson != null && EF.Functions.ILike(x.OldValuesJson, $"%{term}%")) ||
                (x.NewValuesJson != null && EF.Functions.ILike(x.NewValuesJson, $"%{term}%")) ||
                (x.ChangedColumnsJson != null && EF.Functions.ILike(x.ChangedColumnsJson, $"%{term}%"))
            );
        }

        return qset;
    }

    private static DateTime ToUtcFrom(DateTime dt)
    {
        if (dt.Kind == DateTimeKind.Utc)
            return dt;

        if (dt.Kind == DateTimeKind.Unspecified)
            dt = DateTime.SpecifyKind(dt, DateTimeKind.Local);

        return dt.ToUniversalTime();
    }

    private static DateTime ToUtcToInclusive(DateTime dt)
    {
        var value = dt;

        if (dt.TimeOfDay == TimeSpan.Zero)
            value = dt.Date.AddDays(1).AddTicks(-1);

        return ToUtcFrom(value);
    }

    private static string PrettyJsonOrOriginal(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);

            var prettyOpts = new JsonSerializerOptions(JsonOpts)
            {
                WriteIndented = true
            };

            return JsonSerializer.Serialize(doc.RootElement, prettyOpts);
        }
        catch
        {
            return json;
        }
    }
}