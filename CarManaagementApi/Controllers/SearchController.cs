using CarManaagementApi.Persistence;
using CarManaagementApi.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarManaagementApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/search")]
public class SearchController : ApiControllerBase
{
    private readonly RentXDbContext _db;

    public SearchController(RentXDbContext db)
    {
        _db = db;
    }

    [HttpGet("global")]
    public async Task<IActionResult> GlobalSearch([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return OkResponse<IEnumerable<object>>([]);
        }

        var bookingResults = await _db.Bookings
            .AsNoTracking()
            .Include(x => x.Customer)
            .Where(x => x.BookingId.Contains(q) || x.Customer.Name.Contains(q))
            .Take(5)
            .Select(x => (object)new
            {
                type = "booking",
                id = x.BookingId,
                title = x.BookingId + " - " + x.Customer.Name,
                route = "/layout/manage-bookings"
            })
            .ToListAsync();

        var customerResults = await _db.Customers
            .AsNoTracking()
            .Where(x => x.Name.Contains(q) || x.CustomerId.Contains(q) || x.Phone.Contains(q))
            .Take(5)
            .Select(x => (object)new
            {
                type = "customer",
                id = x.CustomerId,
                title = x.Name,
                route = "/layout/customers"
            })
            .ToListAsync();

        var carResults = await _db.Cars
            .AsNoTracking()
            .Where(x => x.CarId.Contains(q) || x.Brand.Contains(q) || x.Model.Contains(q))
            .Take(5)
            .Select(x => (object)new
            {
                type = "car",
                id = x.CarId,
                title = x.Brand + " " + x.Model,
                route = "/layout/car-master"
            })
            .ToListAsync();

        var data = bookingResults
            .Concat(customerResults)
            .Concat(carResults)
            .Take(20)
            .ToList();

        return OkResponse<IEnumerable<object>>(data);
    }
}
