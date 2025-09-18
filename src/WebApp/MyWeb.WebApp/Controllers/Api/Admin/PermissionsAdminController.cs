using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyWeb.Infrastructure.Data.Identity;

namespace MyWeb.WebApp.Controllers.Api.Admin
{
    [ApiController]
    [Route("api/admin/permissions")]
    [Authorize(Roles = "Admin")]
    public sealed class PermissionsAdminController : ControllerBase
    {
        private readonly IdentityDb _auth;

        public PermissionsAdminController(IdentityDb auth) => _auth = auth;

        // ---- DTOs (VALIDATION: attribute'lar primary ctor PARAM'larında, property target ile) ----
        public sealed record CreateSetDto(
            [property: Required, MaxLength(200)] string Name,
            [property: MaxLength(256)] string? Description
        );

        public sealed record CreatePermDto(
            [property: Required] Guid PermissionSetId,
            [property: Required] PermissionScopeType ScopeType,
            [property: Required, MaxLength(128)] string ScopeId,
            [property: Required] PermissionAccess Access
        );

        public sealed record AssignUserSetDto(
            [property: Required] string UserId,
            [property: Required] Guid PermissionSetId
        );

        public sealed record AssignRoleSetDto(
            [property: Required] string RoleId,
            [property: Required] Guid PermissionSetId
        );

        // ---- Permission Set CRUD (minimal) ----
        [HttpPost("sets")]
        public async Task<IActionResult> CreateSet([FromBody] CreateSetDto dto)
        {
            // isim tekilliği (idempotent davranış)
            var exists = await _auth.PermissionSets.AnyAsync(x => x.Name == dto.Name);
            if (exists) return Conflict($"PermissionSet '{dto.Name}' already exists.");

            var set = new PermissionSet { Name = dto.Name, Description = dto.Description };
            _auth.PermissionSets.Add(set);
            await _auth.SaveChangesAsync();
            return CreatedAtAction(nameof(GetSet), new { id = set.Id }, set);
        }

        [HttpGet("sets/{id:guid}")]
        public async Task<IActionResult> GetSet([FromRoute] Guid id)
        {
            var set = await _auth.PermissionSets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            return set is null ? NotFound() : Ok(set);
        }

        [HttpGet("sets")]
        public async Task<IActionResult> ListSets()
        {
            var list = await _auth.PermissionSets.AsNoTracking().OrderBy(x => x.Name).ToListAsync();
            return Ok(list);
        }

        // ---- Permission ekleme ----
        [HttpPost("perms")]
        public async Task<IActionResult> CreatePermission([FromBody] CreatePermDto dto)
        {
            var setExists = await _auth.PermissionSets.AnyAsync(x => x.Id == dto.PermissionSetId);
            if (!setExists) return BadRequest("PermissionSet not found");

            // duplicate kontrolü (aynı set+scope+access)
            var dup = await _auth.Permissions.AnyAsync(p =>
                p.PermissionSetId == dto.PermissionSetId &&
                p.ScopeType == dto.ScopeType &&
                p.ScopeId == dto.ScopeId &&
                p.Access == dto.Access);

            if (dup) return Conflict("Permission already exists for given scope in this set.");

            var p = new Permission
            {
                PermissionSetId = dto.PermissionSetId,
                ScopeType = dto.ScopeType,
                ScopeId = dto.ScopeId,
                Access = dto.Access
            };
            _auth.Permissions.Add(p);
            await _auth.SaveChangesAsync();
            return Ok(p);
        }

        // ---- Set atama (user/role) ----
        [HttpPost("assign/user")]
        public async Task<IActionResult> AssignToUser([FromBody] AssignUserSetDto dto)
        {
            var setExists = await _auth.PermissionSets.AnyAsync(x => x.Id == dto.PermissionSetId);
            if (!setExists) return BadRequest("PermissionSet not found");

            var already = await _auth.UserPermissionSets
                .AnyAsync(x => x.UserId == dto.UserId && x.PermissionSetId == dto.PermissionSetId);

            if (!already)
            {
                _auth.UserPermissionSets.Add(new UserPermissionSet
                {
                    UserId = dto.UserId,
                    PermissionSetId = dto.PermissionSetId
                });
                await _auth.SaveChangesAsync();
            }
            return Ok(new { assigned = true });
        }

        [HttpPost("assign/role")]
        public async Task<IActionResult> AssignToRole([FromBody] AssignRoleSetDto dto)
        {
            var setExists = await _auth.PermissionSets.AnyAsync(x => x.Id == dto.PermissionSetId);
            if (!setExists) return BadRequest("PermissionSet not found");

            var already = await _auth.RolePermissionSets
                .AnyAsync(x => x.RoleId == dto.RoleId && x.PermissionSetId == dto.PermissionSetId);

            if (!already)
            {
                _auth.RolePermissionSets.Add(new RolePermissionSet
                {
                    RoleId = dto.RoleId,
                    PermissionSetId = dto.PermissionSetId
                });
                await _auth.SaveChangesAsync();
            }
            return Ok(new { assigned = true });
        }
    }
}
