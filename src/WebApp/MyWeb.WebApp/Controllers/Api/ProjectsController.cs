using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyWeb.Persistence.Catalog;

namespace MyWeb.WebApp.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProjectsController : ControllerBase
    {
        private readonly CatalogDbContext _db;

        public ProjectsController(CatalogDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Tüm projeler (özet)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllAsync(CancellationToken ct)
        {
            // En basit haliyle Project tablosu + (opsiyonel) Tag/Config sayıları
            var data = await _db.Projects
                .Select(p => new
                {
                    p.Id,
                    p.Key,
                    p.Name,
                    p.Version,
                    p.MinEngine,
                    p.CreatedUtc,
                    // İsteğe bağlı sayımlar:
                    TagCount = _db.Tags.Count(t => t.ProjectId == p.Id),
                    TagCfgs = _db.TagArchiveConfigs.Count(c => c.Tag!.ProjectId == p.Id)
                })
                .OrderBy(p => p.Id)
                .ToListAsync(ct);

            return Ok(data);
        }

        /// <summary>
        /// Tek proje (id veya key ile)
        /// </summary>
        /// <param name="idOrKey">Ör: 1 veya Demo.Plant</param>
        [HttpGet("{idOrKey}")]
        public async Task<IActionResult> GetOneAsync(string idOrKey, CancellationToken ct)
        {
            // id numeric mi key mi?
            bool isId = int.TryParse(idOrKey, out var id);
            var q = _db.Projects.AsQueryable();

            var proj = await q
                .Where(p => isId ? p.Id == id : p.Key == idOrKey)
                .Select(p => new
                {
                    p.Id,
                    p.Key,
                    p.Name,
                    p.Version,
                    p.MinEngine,
                    p.CreatedUtc,
                    Versions = _db.ProjectVersions
                                  .Where(v => v.ProjectId == p.Id)
                                  .OrderByDescending(v => v.AppliedUtc)
                                  .Select(v => new { v.Id, v.Version, v.AppliedUtc, v.Hash })
                                  .ToList()
                })
                .FirstOrDefaultAsync(ct);

            if (proj == null) return NotFound();
            return Ok(proj);
        }
    }
}
