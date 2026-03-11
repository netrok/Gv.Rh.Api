using ClosedXML.Excel;
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

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public AuditController(RhDbContext db)
    {
        _db = db;
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
            item.OldValuesJson,
            item.NewValuesJson,
            item.ChangedColumnsJson
        });
    }

    // GET /api/audit/export.xlsx?from=...&to=...&entityName=...&action=...&email=...&q=...
    [HttpGet("export.xlsx")]
    public async Task<IActionResult> ExportXlsx(
        [FromQuery] string? entityName = null,
        [FromQuery] string? recordId = null,
        [FromQuery] string? action = null,
        [FromQuery] string? email = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? q = null)
    {
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
            .ThenByDescending(x => x.Id)
            .Take(50000);

        var rows = await query
            .Select(x => new
            {
                x.Id,
                x.OccurredAtUtc,
                x.UserEmail,
                x.UserRole,
                x.Action,
                x.EntityName,
                x.RecordId,
                x.IpAddress,
                x.UserAgent,
                x.OldValuesJson,
                x.NewValuesJson,
                x.ChangedColumnsJson
            })
            .ToListAsync();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Audit");

        var headers = new[]
        {
            "Id",
            "OccurredAtUtc",
            "UserEmail",
            "UserRole",
            "Action",
            "EntityName",
            "RecordId",
            "IpAddress",
            "UserAgent",
            "OldValuesJson",
            "NewValuesJson",
            "ChangedColumnsJson"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
            ws.Cell(1, i + 1).Style.Font.Bold = true;
        }

        var r = 2;
        foreach (var x in rows)
        {
            ws.Cell(r, 1).Value = x.Id;
            ws.Cell(r, 2).Value = x.OccurredAtUtc;
            ws.Cell(r, 3).Value = x.UserEmail ?? "";
            ws.Cell(r, 4).Value = x.UserRole ?? "";
            ws.Cell(r, 5).Value = x.Action;
            ws.Cell(r, 6).Value = x.EntityName;
            ws.Cell(r, 7).Value = x.RecordId;
            ws.Cell(r, 8).Value = x.IpAddress ?? "";
            ws.Cell(r, 9).Value = x.UserAgent ?? "";
            ws.Cell(r, 10).Value = x.OldValuesJson ?? "";
            ws.Cell(r, 11).Value = x.NewValuesJson ?? "";
            ws.Cell(r, 12).Value = x.ChangedColumnsJson ?? "";
            r++;
        }

        ws.Column(2).Style.DateFormat.Format = "yyyy-mm-dd HH:mm:ss";
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);

        var bytes = ms.ToArray();
        var fileName = BuildAuditFileName(from, to, entityName, action);

        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
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
        var v = dt;

        if (dt.TimeOfDay == TimeSpan.Zero)
            v = dt.Date.AddDays(1).AddTicks(-1);

        return ToUtcFrom(v);
    }

    private static string BuildAuditFileName(DateTime? from, DateTime? to, string? entityName, string? action)
    {
        static string Safe(string? s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return "";

            var t = s.Trim();
            foreach (var c in Path.GetInvalidFileNameChars())
                t = t.Replace(c, '_');

            return t.Length > 40 ? t[..40] : t;
        }

        var parts = new List<string> { "audit" };

        if (from.HasValue || to.HasValue)
        {
            var f = (from ?? DateTime.UtcNow).Date;
            var t = (to ?? DateTime.UtcNow).Date;
            parts.Add($"{f:yyyyMMdd}-{t:yyyyMMdd}");
        }

        if (!string.IsNullOrWhiteSpace(entityName))
            parts.Add(Safe(entityName));

        if (!string.IsNullOrWhiteSpace(action))
            parts.Add(Safe(action));

        parts.Add(DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));

        return string.Join("_", parts) + ".xlsx";
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