using CarManaagementApi.Contracts;
using CarManaagementApi.Services;
using CarManaagementApi.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarManaagementApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/lookups")]
public class LookupsController : ApiControllerBase
{
    private readonly IRentXStore _store;

    public LookupsController(IRentXStore store)
    {
        _store = store;
    }

    [HttpGet("branches")]
    public IActionResult GetBranches()
    {
        List<object> branches;
        lock (_store.SyncRoot)
        {
            branches = _store.Branches
                .Where(x => x.Active)
                .Select(x => (object)new { code = x.Id, name = x.Name })
                .ToList();
        }

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
