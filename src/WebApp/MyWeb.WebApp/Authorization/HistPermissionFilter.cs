using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MyWeb.WebApp.Authorization
{
    public sealed class HistPermissionFilter : IAsyncActionFilter
    {
        private readonly ITagPermissionService _svc;
        public HistPermissionFilter(ITagPermissionService svc) => _svc = svc;

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var path = (context.HttpContext.Request.Path.Value ?? string.Empty).ToLowerInvariant();

            if (path.StartsWith("/api/hist"))
            {
                if (!(context.HttpContext.User?.Identity?.IsAuthenticated ?? false))
                {
                    context.Result = new UnauthorizedResult();
                    return;
                }

                string? userId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
                string tagIdRaw = context.HttpContext.Request.Query["tagId"].FirstOrDefault() ?? string.Empty;

                if (int.TryParse(tagIdRaw, out var tagId))
                {
                    var allowed = await _svc.CanReadTagAsync(userId, context.HttpContext.User, tagId);
                    if (!allowed) { context.Result = new ForbidResult(); return; }
                }
            }

            await next();
        }
    }
}
