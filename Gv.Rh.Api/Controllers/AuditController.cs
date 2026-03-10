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

    // Json options base (sin indentado)
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public AuditController(RhDbContext db) => _db = db;

    // GET /api/audit?entity=Empleado&entityId=123&action=UPDATE&email=rrhh@rh.local&from=2026-03-01&to=2026-03-04&q=algo&page=1&pageSize=50
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? entity = null,
        [FromQuery] string? entityId = null,
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

        var query = ApplyFilters(_db.AuditLogs.AsNoTracking(), entity, entityId, action, email, from, to, q)
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
                x.Entity,
                x.EntityId,
                x.UserId,
                x.Email,
                x.Role,
                x.Ip,
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
        var item = await _db.AuditLogs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (item is null) return NotFound();

        var changes = item.ChangesJson;
        if (pretty && !string.IsNullOrWhiteSpace(changes))
            changes = PrettyJsonOrOriginal(changes);

        return Ok(new
        {
            item.Id,
            item.OccurredAtUtc,
            item.UserId,
            item.Email,
            item.Role,
            item.Action,
            item.Entity,
            item.EntityId,
            item.Ip,
            item.UserAgent,
            ChangesJson = changes
        });
    }

    // GET /api/audit/export.xlsx?from=...&to=...&entity=...&action=...&email=...&q=...
    [HttpGet("export.xlsx")]
    public async Task<IActionResult> ExportXlsx(
        [FromQuery] string? entity = null,
        [FromQuery] string? entityId = null,
        [FromQuery] string? action = null,
        [FromQuery] string? email = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? q = null)
    {
        var query = ApplyFilters(_db.AuditLogs.AsNoTracking(), entity, entityId, action, email, from, to, q)
            .OrderByDescending(x => x.OccurredAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(50000); // límite sano

        var rows = await query
            .Select(x => new
            {
                x.Id,
                x.OccurredAtUtc,
                x.Email,
                x.Role,
                x.Action,
                x.Entity,
                x.EntityId,
                x.Ip,
                x.UserAgent,
                x.ChangesJson
            })
            .ToListAsync();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Audit");

        var headers = new[]
        {
            "Id", "OccurredAtUtc", "Email", "Role", "Action",
            "Entity", "EntityId", "IP", "UserAgent", "ChangesJson"
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
            ws.Cell(r, 3).Value = x.Email ?? "";
            ws.Cell(r, 4).Value = x.Role ?? "";
            ws.Cell(r, 5).Value = x.Action;
            ws.Cell(r, 6).Value = x.Entity;
            ws.Cell(r, 7).Value = x.EntityId;
            ws.Cell(r, 8).Value = x.Ip ?? "";
            ws.Cell(r, 9).Value = x.UserAgent ?? "";
            ws.Cell(r, 10).Value = x.ChangesJson ?? "";
            r++;
        }

        ws.Column(2).Style.DateFormat.Format = "yyyy-mm-dd HH:mm:ss";
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);

        var bytes = ms.ToArray();
        var fileName = BuildAuditFileName(from, to, entity, action);

        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    private static IQueryable<AuditLog> ApplyFilters(
        IQueryable<AuditLog> qset,
        string? entity,
        string? entityId,
        string? action,
        string? email,
        DateTime? from,
        DateTime? to,
        string? q)
    {
        if (!string.IsNullOrWhiteSpace(entity))
            qset = qset.Where(x => x.Entity == entity.Trim());

        if (!string.IsNullOrWhiteSpace(entityId))
            qset = qset.Where(x => x.EntityId == entityId.Trim());

        if (!string.IsNullOrWhiteSpace(action))
            qset = qset.Where(x => x.Action == action.Trim());

        if (!string.IsNullOrWhiteSpace(email))
        {
            var term = email.Trim();
            qset = qset.Where(x => x.Email != null && EF.Functions.ILike(x.Email, $"%{term}%"));
        }

        if (from.HasValue)
            qset = qset.Where(x => x.OccurredAtUtc >= ToUtcFrom(from.Value));

        if (to.HasValue)
            qset = qset.Where(x => x.OccurredAtUtc <= ToUtcToInclusive(to.Value));

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            qset = qset.Where(x =>
                (x.Email != null && EF.Functions.ILike(x.Email, $"%{term}%")) ||
                EF.Functions.ILike(x.Entity, $"%{term}%") ||
                EF.Functions.ILike(x.Action, $"%{term}%") ||
                (x.EntityId != null && EF.Functions.ILike(x.EntityId, $"%{term}%")) ||
                (x.ChangesJson != null && EF.Functions.ILike(x.ChangesJson, $"%{term}%"))
            );
        }

        return qset;
    }

    private static DateTime ToUtcFrom(DateTime dt)
    {
        if (dt.Kind == DateTimeKind.Utc) return dt;

        // Si viene Unspecified (ej. "2026-03-01"), lo tratamos como local del server
        if (dt.Kind == DateTimeKind.Unspecified)
            dt = DateTime.SpecifyKind(dt, DateTimeKind.Local);

        return dt.ToUniversalTime();
    }

    private static DateTime ToUtcToInclusive(DateTime dt)
    {
        // Si te pasan solo fecha (00:00:00), lo tomamos como fin del día
        var v = dt;
        if (dt.TimeOfDay == TimeSpan.Zero)
            v = dt.Date.AddDays(1).AddTicks(-1);

        return ToUtcFrom(v);
    }

    private static string BuildAuditFileName(DateTime? from, DateTime? to, string? entity, string? action)
    {
        static string Safe(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            var t = s.Trim();
            foreach (var c in Path.GetInvalidFileNameChars()) t = t.Replace(c, '_');
            return t.Length > 40 ? t[..40] : t;
        }

        var parts = new List<string> { "audit" };

        if (from.HasValue || to.HasValue)
        {
            var f = (from ?? DateTime.UtcNow).Date;
            var t = (to ?? DateTime.UtcNow).Date;
            parts.Add($"{f:yyyyMMdd}-{t:yyyyMMdd}");
        }

        if (!string.IsNullOrWhiteSpace(entity)) parts.Add(Safe(entity));
        if (!string.IsNullOrWhiteSpace(action)) parts.Add(Safe(action));

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