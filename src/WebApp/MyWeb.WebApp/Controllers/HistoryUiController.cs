using Microsoft.AspNetCore.Authorization;
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
        [AllowAnonymous]
        [HttpGet("trend")]
        public async Task<IActionResult> Trend([FromQuery] string? projectKey = null, CancellationToken ct = default)
        {
            var projects = await _catalog.Projects
                .AsNoTracking()
                .OrderBy(p => p.Id)
                .Select(p => new ProjectVm { Id = p.Id, Key = p.Key!, Name = p.Name! })
                .ToListAsync(ct);

            int selectedProjectId;
            if (!string.IsNullOrWhiteSpace(projectKey))
            {
                selectedProjectId = await _catalog.Projects
                    .Where(p => p.Key == projectKey)
                    .Select(p => p.Id)
                    .FirstOrDefaultAsync(ct);

                if (selectedProjectId == 0)
                    selectedProjectId = projects.FirstOrDefault()?.Id ?? 0;
            }
            else
            {
                selectedProjectId = projects.FirstOrDefault()?.Id ?? 0;
            }

            var tags = await _catalog.Tags
                .AsNoTracking()
                .Where(t => selectedProjectId == 0 || t.ProjectId == selectedProjectId)
                .OrderBy(t => t.Path)
                .Select(t => new TagVm
                {
                    Id = t.Id,
                    Path = t.Path!,             // View bunu bekliyor
                    Name = t.Name!,
                    DataType = (int)t.DataType  // enum -> int
                })
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

        // ==== ViewModel'ler (Razor 'HistoryUiController.TrendVm' bekliyor) ====
        public sealed record ProjectVm
        {
            public int Id { get; set; }
            public string Key { get; set; } = "";
            public string Name { get; set; } = "";
        }

        public sealed record TagVm
        {
            public int Id { get; set; }
            public string Path { get; set; } = "";
            public string Name { get; set; } = "";
            public int DataType { get; set; }
        }

        public sealed record TrendVm
        {
            public List<ProjectVm> Projects { get; set; } = new();
            public List<TagVm> Tags { get; set; } = new();
            public int SelectedProjectId { get; set; }
            public string? ProjectKey { get; set; }
        }
    }
}
