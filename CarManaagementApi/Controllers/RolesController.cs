using CarManaagementApi.Contracts;
using CarManaagementApi.Services;
using CarManaagementApi.Services.Models;
using CarManaagementApi.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarManaagementApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/roles")]
public class RolesController : ApiControllerBase
{
    private readonly IRentXStore _store;

    public RolesController(IRentXStore store)
    {
        _store = store;
    }

    [HttpGet("permissions")]
    public IActionResult GetPermissions()
    {
        lock (_store.SyncRoot)
        {
            var payload = _store.RolePermissions.ToDictionary(
                role => role.Key,
                role => (object)role.Value.ToDictionary(
                    module => module.Key,
                    module => (object)new
                    {
                        view = module.Value.View,
                        create = module.Value.Create,
                        edit = module.Value.Edit,
                        delete = module.Value.Delete,
                        approve = module.Value.Approve
                    }),
                StringComparer.OrdinalIgnoreCase);

            return OkResponse((object)payload);
        }
    }

    [HttpPut("{role}/permissions")]
    public IActionResult UpdatePermissions(string role, Dictionary<string, PermissionInput> request)
    {
        if (!RentXConstants.IsValid(RentXConstants.Roles, role))
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, "Invalid role");
        }

        lock (_store.SyncRoot)
        {
            var mapped = request.ToDictionary(
                x => x.Key,
                x => new PermissionRecord
                {
                    View = x.Value.View,
                    Create = x.Value.Create,
                    Edit = x.Value.Edit,
                    Delete = x.Value.Delete,
                    Approve = x.Value.Approve
                },
                StringComparer.OrdinalIgnoreCase);

            _store.RolePermissions[role.ToLowerInvariant()] = mapped;

            return OkResponse(new
            {
                role = role.ToLowerInvariant(),
                permissions = mapped
            }, "Role permissions updated");
        }
    }

    [HttpPost("{role}/reset-default")]
    public IActionResult ResetDefault(string role)
    {
        if (!RentXConstants.IsValid(RentXConstants.Roles, role))
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, "Invalid role");
        }

        lock (_store.SyncRoot)
        {
            var defaultPermissions = BuildDefaults(role.ToLowerInvariant());
            _store.RolePermissions[role.ToLowerInvariant()] = defaultPermissions;
            return OkResponse(new
            {
                role = role.ToLowerInvariant(),
                permissions = defaultPermissions
            }, "Role permissions reset to default");
        }
    }

    private static Dictionary<string, PermissionRecord> BuildDefaults(string role)
    {
        var modules = new[] { "Bookings", "Cars", "Customers", "Branches", "Maintenance", "Reports" };
        return modules.ToDictionary(
            x => x,
            _ => role switch
            {
                "admin" => new PermissionRecord { View = true, Create = true, Edit = true, Delete = true, Approve = true },
                "ops" => new PermissionRecord { View = true, Create = true, Edit = true, Delete = false, Approve = true },
                "agent" => new PermissionRecord { View = true, Create = true, Edit = false, Delete = false, Approve = false },
                _ => new PermissionRecord { View = true, Create = false, Edit = false, Delete = false, Approve = false }
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
