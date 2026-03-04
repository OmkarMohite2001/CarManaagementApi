using CarManaagementApi.Contracts;
using CarManaagementApi.Persistence;
using CarManaagementApi.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarManaagementApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/lookups")]
public class LookupsController : ApiControllerBase
{
    private readonly RentXDbContext _db;

    public LookupsController(RentXDbContext db)
    {
        _db = db;
    }

    [HttpGet("branches")]
    public async Task<IActionResult> GetBranches()
    {
        var branches = await _db.Branches
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => (object)new { code = x.BranchId, name = x.Name })
            .ToListAsync();

        return OkResponse<IEnumerable<object>>(branches);
    }

    [HttpGet("enums")]
    public IActionResult GetEnums()
    {
        return OkResponse(new
        {
            carTypes = RentXConstants.CarTypes,
            transmissions = RentXConstants.Transmissions,
            fuels = RentXConstants.Fuels,
            bookingStatuses = RentXConstants.BookingStatuses,
            maintenanceTypes = RentXConstants.MaintenanceTypes,
            roles = RentXConstants.Roles,
            kycTypes = RentXConstants.KycTypes
        });
    }
}
