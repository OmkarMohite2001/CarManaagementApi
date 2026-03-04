using CarManaagementApi.Services;
using CarManaagementApi.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarManaagementApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/search")]
public class SearchController : ApiControllerBase
{
    private readonly IRentXStore _store;

    public SearchController(IRentXStore store)
    {
        _store = store;
    }

    [HttpGet("global")]
    public IActionResult GlobalSearch([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return OkResponse<IEnumerable<object>>([]);
        }

        lock (_store.SyncRoot)
        {
            var bookingResults = _store.Bookings
                .Where(x => x.Id.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || x.CustomerName.Contains(q, StringComparison.OrdinalIgnoreCase))
                .Take(5)
                .Select(x => (object)new
                {
                    type = "booking",
                    id = x.Id,
                    title = $"{x.Id} - {x.CustomerName}",
                    route = "/layout/manage-bookings"
                });

            var customerResults = _store.Customers
                .Where(x => x.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || x.Id.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || x.Phone.Contains(q, StringComparison.OrdinalIgnoreCase))
                .Take(5)
                .Select(x => (object)new
                {
                    type = "customer",
                    id = x.Id,
                    title = x.Name,
                    route = "/layout/customers"
                });

            var carResults = _store.Cars
                .Where(x => x.Id.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || x.Brand.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || x.Model.Contains(q, StringComparison.OrdinalIgnoreCase))
                .Take(5)
                .Select(x => (object)new
                {
                    type = "car",
                    id = x.Id,
                    title = $"{x.Brand} {x.Model}",
                    route = "/layout/car-master"
                });

            var data = bookingResults
                .Concat(customerResults)
                .Concat(carResults)
                .Take(20)
                .ToList();

            return OkResponse<IEnumerable<object>>(data);
        }
    }
}
