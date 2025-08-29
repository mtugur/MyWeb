using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyWeb.Persistence.Catalog;

namespace MyWeb.WebApp.Controllers
{
    [Route("history")]
    public sealed class HistoryUiController : Controller
    {
        private readonly CatalogDbContext _catalog;

        public HistoryUiController(CatalogDbContext catalog)
        {
            _catalog = catalog;
        }

        // /history/trend?projectKey=Demo.Plant
        [HttpGet("trend")]
        public async Task<IActionResult> Trend([FromQuery] string? projectKey = null, CancellationToken ct = default)
        {
            var projects = await _catalog.Projects
                .AsNoTracking()
                .OrderBy(p => p.Id)
                .Select(p => new ProjectVm { Id = p.Id, Key = p.Key!, Name = p.Name! })
                .ToListAsync(ct);

            if (projects.Count == 0)
            {
                ViewData["Error"] = "Katalogda proje bulunamadı. Bootstrap/demo SQL’ini çalıştırın.";
                return View("Trend", new TrendVm());
            }

            int selectedProjectId =
                projects.FirstOrDefault(p => !string.IsNullOrWhiteSpace(projectKey) && p.Key == projectKey)?.Id
                ?? projects[0].Id;

            var tags = await _catalog.Tags
                .AsNoTracking()
                .Where(t => t.ProjectId == selectedProjectId)
                .OrderBy(t => t.Path)
                .Select(t => new TagVm { Id = t.Id, Path = t.Path!, Name = t.Name!, DataType = (int)t.DataType })
                .ToListAsync(ct);

            var vm = new TrendVm
            {
                Projects = projects,
                Tags = tags,
                SelectedProjectId = selectedProjectId,
                ProjectKey = projectKey
            };

            return View("~/Views/HistoryUi/Trend.cshtml", vm);
        }

        // basit view-model’ler
        public sealed record ProjectVm { public int Id { get; set; } public string Key { get; set; } = ""; public string Name { get; set; } = ""; }
        public sealed record TagVm { public int Id { get; set; } public string Path { get; set; } = ""; public string Name { get; set; } = ""; public int DataType { get; set; } }
        public sealed record TrendVm
        {
            public List<ProjectVm> Projects { get; set; } = new();
            public List<TagVm> Tags { get; set; } = new();
            public int SelectedProjectId { get; set; }
            public string? ProjectKey { get; set; }
        }
    }
}
