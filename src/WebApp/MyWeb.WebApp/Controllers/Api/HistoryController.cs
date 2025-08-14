using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyWeb.Persistence.Catalog;
using MyWeb.Persistence.Historian;

namespace MyWeb.WebApp.Controllers.Api
{
    [ApiController]
    [Route("api/hist")]
    public class HistoryController : ControllerBase
    {
        private readonly CatalogDbContext _catalog;
        private readonly HistorianDbContext _hist;

        public HistoryController(CatalogDbContext catalog, HistorianDbContext hist)
        {
            _catalog = catalog;
            _hist = hist;
        }

        [HttpGet("projects")]
        public async Task<IActionResult> GetProjects()
        {
            var data = await _catalog.Projects
                .Select(p => new { p.Id, p.Key, p.Name, p.Version })
                .ToListAsync();
            return Ok(data);
        }

        [HttpGet("tags")]
        public async Task<IActionResult> GetTags([FromQuery] int projectId)
        {
            var data = await (
                from t in _catalog.Tags
                join a in _catalog.TagArchiveConfigs on t.Id equals a.TagId into ag
                from a in ag.DefaultIfEmpty()
                where t.ProjectId == projectId
                orderby t.Path
                select new
                {
                    t.Id,
                    t.Path,
                    t.Name,
                    t.DataType,
                    t.Unit,
                    t.Address,
                    Archive = a == null ? null : new
                    {
                        a.Mode,
                        a.DeadbandAbs,
                        a.DeadbandPercent,
                        a.RetentionDays,
                        a.RollupsJson
                    }
                }).ToListAsync();

            return Ok(data);
        }

        // Ör: /api/hist/samples?tagId=1&from=2025-08-13T08:50:00Z&take=200
        [HttpGet("samples")]
        public async Task<IActionResult> GetSamples(
            [FromQuery] long tagId,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int take = 1000)
        {
            var q = _hist.Samples.Where(s => s.TagId == tagId);
            if (from.HasValue) q = q.Where(s => s.Utc >= from.Value);
            if (to.HasValue) q = q.Where(s => s.Utc <= to.Value);

            var list = await q
                .OrderByDescending(s => s.Utc)
                .Take(Math.Clamp(take, 1, 5000))
                .OrderBy(s => s.Utc)
                .Select(s => new
                {
                    s.Utc,
                    s.DataType,
                    s.ValueNumeric,
                    s.ValueText,
                    s.ValueBool,
                    s.Quality,
                    s.Source
                })
                .ToListAsync();

            return Ok(list);
        }
    }
}
