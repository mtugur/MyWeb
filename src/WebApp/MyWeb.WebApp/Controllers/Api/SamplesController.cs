using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyWeb.Persistence.Catalog;
using MyWeb.Persistence.Historian;
using MyWeb.Core.Hist;
using System.Linq;

namespace MyWeb.WebApp.Api;

[ApiController]
[Route("api/[controller]")]
public class SamplesController : ControllerBase
{
    private readonly HistorianDbContext _hist;
    private readonly CatalogDbContext _catalog;

    public SamplesController(HistorianDbContext hist, CatalogDbContext catalog)
    {
        _hist = hist;
        _catalog = catalog;
    }

    public record SampleDto(
    long Id, int TagId, DateTime Utc, short Quality, DataType DataType,
    double? ValueNumeric, string? ValueText, bool? ValueBool, int MonthKey);

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] int? tagId,
        [FromQuery] string? tagPath,
        [FromQuery] string? projectKey,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 2000) pageSize = 100;

        int? resolvedTagId = tagId;
        if (resolvedTagId is null)
        {
            int projectId;
            if (!string.IsNullOrEmpty(projectKey))
            {
                projectId = await _catalog.Projects
                    .Where(p => p.Key == projectKey)
                    .Select(p => p.Id)
                    .FirstOrDefaultAsync();

                if (projectId == 0)
                    return NotFound($"ProjectKey '{projectKey}' bulunamadı.");
            }
            else
            {
                projectId = await _catalog.Projects
                    .OrderBy(p => p.Id)
                    .Select(p => p.Id)
                    .FirstOrDefaultAsync();

                if (projectId == 0)
                    return NotFound("Herhangi bir proje bulunamadı.");
            }

            if (!string.IsNullOrEmpty(tagPath))
            {
                resolvedTagId = await _catalog.Tags
                    .Where(t => t.ProjectId == projectId && t.Path == tagPath)
                    .Select(t => (int?)t.Id)
                    .FirstOrDefaultAsync();

                if (resolvedTagId is null)
                    return NotFound($"TagPath '{tagPath}' bulunamadı.");
            }
            else
            {
                resolvedTagId = await _catalog.Tags
                    .Where(t => t.ProjectId == projectId)
                    .OrderBy(t => t.Id)
                    .Select(t => (int?)t.Id)
                    .FirstOrDefaultAsync();

                if (resolvedTagId is null)
                    return NotFound("Projede tag yok.");
            }
        }

        var q = _hist.Samples.AsNoTracking().Where(s => s.TagId == resolvedTagId);

        if (from.HasValue) q = q.Where(s => s.Utc >= from.Value);
        if (to.HasValue) q = q.Where(s => s.Utc <= to.Value);

        var total = await q.CountAsync();
        var items = await q
            .OrderByDescending(s => s.Utc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SampleDto(
                s.Id, s.TagId, s.Utc, s.Quality, s.DataType,
                s.ValueNumeric, s.ValueText, s.ValueBool, s.MonthKey))
            .ToListAsync();

        return Ok(new { total, page, pageSize, items });
    }
}
