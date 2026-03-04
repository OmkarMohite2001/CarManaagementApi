using CarManaagementApi.Contracts;
using CarManaagementApi.Persistence;
using CarManaagementApi.Persistence.Entities;
using CarManaagementApi.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarManaagementApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/roles")]
public class RolesController : ApiControllerBase
{
    private readonly RentXDbContext _db;

    public RolesController(RentXDbContext db)
    {
        _db = db;
    }

    [HttpGet("permissions")]
    public async Task<IActionResult> GetPermissions()
    {
        var rows = await _db.RolePermissions
            .AsNoTracking()
            .OrderBy(x => x.RoleCode)
            .ThenBy(x => x.ModuleName)
            .ToListAsync();

        var payload = rows
            .GroupBy(x => x.RoleCode)
            .ToDictionary(
                role => role.Key,
                role => (object)role.ToDictionary(
                    module => module.ModuleName,
                    module => (object)new
                    {
                        view = module.CanView,
                        create = module.CanCreate,
                        edit = module.CanEdit,
                        delete = module.CanDelete,
                        approve = module.CanApprove
                    }),
                StringComparer.OrdinalIgnoreCase);

        return OkResponse((object)payload);
    }

    [HttpPut("{role}/permissions")]
    public async Task<IActionResult> UpdatePermissions(string role, Dictionary<string, PermissionInput> request)
    {
        if (!RentXConstants.IsValid(RentXConstants.Roles, role))
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, "Invalid role");
        }

        var roleCode = role.ToLowerInvariant();
        var existing = await _db.RolePermissions.Where(x => x.RoleCode == roleCode).ToListAsync();
        _db.RolePermissions.RemoveRange(existing);

        foreach (var item in request)
        {
            _db.RolePermissions.Add(new RolePermission
            {
                RoleCode = roleCode,
                ModuleName = item.Key,
                CanView = item.Value.View,
                CanCreate = item.Value.Create,
                CanEdit = item.Value.Edit,
                CanDelete = item.Value.Delete,
                CanApprove = item.Value.Approve,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();

        return OkResponse(new
        {
            role = roleCode,
            permissions = request
        }, "Role permissions updated");
    }

    [HttpPost("{role}/reset-default")]
    public async Task<IActionResult> ResetDefault(string role)
    {
        if (!RentXConstants.IsValid(RentXConstants.Roles, role))
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, "Invalid role");
        }

        var roleCode = role.ToLowerInvariant();
        var defaults = BuildDefaults(roleCode);

        var existing = await _db.RolePermissions.Where(x => x.RoleCode == roleCode).ToListAsync();
        _db.RolePermissions.RemoveRange(existing);

        foreach (var item in defaults)
        {
            _db.RolePermissions.Add(new RolePermission
            {
                RoleCode = roleCode,
                ModuleName = item.Key,
                CanView = item.Value.View,
                CanCreate = item.Value.Create,
                CanEdit = item.Value.Edit,
                CanDelete = item.Value.Delete,
                CanApprove = item.Value.Approve,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();

        return OkResponse(new
        {
            role = roleCode,
            permissions = defaults
        }, "Role permissions reset to default");
    }

    private static Dictionary<string, PermissionInput> BuildDefaults(string role)
    {
        var modules = new[] { "Bookings", "Cars", "Customers", "Branches", "Maintenance", "Reports" };
        return modules.ToDictionary(
            x => x,
            _ => role switch
            {
                "admin" => new PermissionInput { View = true, Create = true, Edit = true, Delete = true, Approve = true },
                "ops" => new PermissionInput { View = true, Create = true, Edit = true, Delete = false, Approve = true },
                "agent" => new PermissionInput { View = true, Create = true, Edit = false, Delete = false, Approve = false },
                _ => new PermissionInput { View = true, Create = false, Edit = false, Delete = false, Approve = false }
            },
            StringComparer.OrdinalIgnoreCase);
    }

    public sealed class PermissionInput
    {
        public bool View { get; set; }
        public bool Create { get; set; }
        public bool Edit { get; set; }
        public bool Delete { get; set; }
        public bool Approve { get; set; }
    }
}
