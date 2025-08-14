using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyWeb.Persistence.Catalog;

namespace MyWeb.WebApp.Api;

[ApiController]
[Route("api/[controller]")]
public class TagsController : ControllerBase
{
    private readonly CatalogDbContext _catalog;
    public TagsController(CatalogDbContext catalog) => _catalog = catalog;

    // GET /api/tags
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string? projectKey,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 500) pageSize = 100;

        int projectId;
        if (!string.IsNullOrEmpty(projectKey))
        {
            projectId = await _catalog.Projects
                .Where(p => p.Key == projectKey)
                .Select(p => p.Id)
                .FirstOrDefaultAsync(ct);

            if (projectId == 0)
                return NotFound($"ProjectKey '{projectKey}' bulunamadı.");
        }
        else
        {
            projectId = await _catalog.Projects
                .OrderBy(p => p.Id)
                .Select(p => p.Id)
                .FirstOrDefaultAsync(ct);

            if (projectId == 0)
                return NotFound("Herhangi bir proje bulunamadı.");
        }

        var query =
            from t in _catalog.Tags.AsNoTracking()
            join a in _catalog.TagArchiveConfigs.AsNoTracking()
                on t.Id equals a.TagId into gj
            from a in gj.DefaultIfEmpty()
            where t.ProjectId == projectId
            select new
            {
                t.Id,
                t.ProjectId,
                t.Path,
                t.Name,
                t.DataType,
                t.Unit,
                t.Scale,
                t.Offset,
                t.Address,
                t.DriverRef,
                t.LongString,
                Archive = a == null ? null : new
                {
                    a.Mode,
                    a.DeadbandAbs,
                    a.DeadbandPercent,
                    a.RetentionDays,
                    a.RollupsJson
                }
            };

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(x => x.Path.Contains(q) || x.Name.Contains(q));

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(x => x.Path)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(new { total, page, pageSize, items });
    }

    // DELETE /api/tags/{id}
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        // İlişkili arşiv kaydı varsa önce onu sil
        var arc = await _catalog.TagArchiveConfigs
            .FirstOrDefaultAsync(a => a.TagId == id, ct);
        if (arc != null)
            _catalog.TagArchiveConfigs.Remove(arc);

        // Tag'ı bul ve sil
        var tag = await _catalog.Tags.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tag == null)
            return NotFound(new { message = $"Tag #{id} bulunamadı." });

        _catalog.Tags.Remove(tag);
        await _catalog.SaveChangesAsync(ct);

        return NoContent(); // 204
    }
}
