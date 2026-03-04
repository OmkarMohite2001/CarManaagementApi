using System.Text.RegularExpressions;
using CarManaagementApi.Contracts;
using CarManaagementApi.Services;
using CarManaagementApi.Services.Models;
using CarManaagementApi.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarManaagementApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/admin/cars")]
public class AdminCarsController : ApiControllerBase
{
    private static readonly Regex RegNoRegex = new("^[A-Z]{2}\\d{2}-[A-Z]{2}-\\d{4}$", RegexOptions.Compiled);

    private readonly IRentXStore _store;

    public AdminCarsController(IRentXStore store)
    {
        _store = store;
    }

    [HttpGet]
    public IActionResult GetAdminCars(
        [FromQuery] string? q,
        [FromQuery] string? branchId,
        [FromQuery] string? type,
        [FromQuery] string? fuel,
        [FromQuery] string? transmission,
        [FromQuery] bool? active,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        lock (_store.SyncRoot)
        {
            var query = _store.Cars.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                query = query.Where(x =>
                    x.Id.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || x.Brand.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || x.Model.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || x.RegNo.Contains(q, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(branchId))
            {
                query = query.Where(x => x.BranchId.Equals(branchId, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(type))
            {
                query = query.Where(x => x.Type.Equals(type, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(fuel))
            {
                query = query.Where(x => x.Fuel.Equals(fuel, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(transmission))
            {
                query = query.Where(x => x.Transmission.Equals(transmission, StringComparison.OrdinalIgnoreCase));
            }

            if (active.HasValue)
            {
                query = query.Where(x => x.Active == active.Value);
            }

            var shaped = query
                .OrderBy(x => x.Brand)
                .ThenBy(x => x.Model)
                .Select(ToResponse);

            var (items, meta) = shaped.Paginate(page, pageSize);
            return OkResponse<IEnumerable<object>>(items, meta: meta);
        }
    }

    [HttpPost]
    public IActionResult CreateAdminCar(AdminCarUpsertRequest request)
    {
        var validation = ValidateRequest(request);
        if (validation is not null)
        {
            return validation;
        }

        lock (_store.SyncRoot)
        {
            if (!_store.Branches.Any(x => x.Id.Equals(request.BranchId, StringComparison.OrdinalIgnoreCase)))
            {
                return ErrorResponse(StatusCodes.Status404NotFound, "Branch not found");
            }

            if (_store.Cars.Any(x => x.RegNo.Equals(request.RegNo, StringComparison.OrdinalIgnoreCase)))
            {
                return ErrorResponse(StatusCodes.Status409Conflict, "Duplicate regNo");
            }

            var car = new AdminCarRecord
            {
                Id = _store.NextId("CAR"),
                Brand = request.Brand,
                Model = request.Model,
                Type = request.Type.ToLowerInvariant(),
                Fuel = request.Fuel.ToLowerInvariant(),
                Transmission = request.Transmission.ToLowerInvariant(),
                Seats = request.Seats,
                DailyPrice = request.DailyPrice,
                RegNo = request.RegNo.ToUpperInvariant(),
                Odometer = request.Odometer,
                BranchId = request.BranchId,
                Active = request.Active,
                Rating = 4.5m,
                ImageUrls = request.ImageUrls ?? [],
                ImageUrl = request.ImageUrls?.FirstOrDefault() ?? string.Empty,
                LocationCodes = [request.BranchId]
            };

            _store.Cars.Add(car);
            return OkResponse(ToResponse(car), "Car created");
        }
    }

    [HttpPut("{carId}")]
    public IActionResult UpdateAdminCar(string carId, AdminCarUpsertRequest request)
    {
        var validation = ValidateRequest(request);
        if (validation is not null)
        {
            return validation;
        }

        lock (_store.SyncRoot)
        {
            var existing = _store.Cars.FirstOrDefault(x => x.Id.Equals(carId, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                return ErrorResponse(StatusCodes.Status404NotFound, "Car not found");
            }

            if (!_store.Branches.Any(x => x.Id.Equals(request.BranchId, StringComparison.OrdinalIgnoreCase)))
            {
                return ErrorResponse(StatusCodes.Status404NotFound, "Branch not found");
            }

            if (_store.Cars.Any(x =>
                    !x.Id.Equals(carId, StringComparison.OrdinalIgnoreCase)
                    && x.RegNo.Equals(request.RegNo, StringComparison.OrdinalIgnoreCase)))
            {
                return ErrorResponse(StatusCodes.Status409Conflict, "Duplicate regNo");
            }

            existing.Brand = request.Brand;
            existing.Model = request.Model;
            existing.Type = request.Type.ToLowerInvariant();
            existing.Fuel = request.Fuel.ToLowerInvariant();
            existing.Transmission = request.Transmission.ToLowerInvariant();
            existing.Seats = request.Seats;
            existing.DailyPrice = request.DailyPrice;
            existing.RegNo = request.RegNo.ToUpperInvariant();
            existing.Odometer = request.Odometer;
            existing.BranchId = request.BranchId;
            existing.Active = request.Active;
            existing.ImageUrls = request.ImageUrls ?? existing.ImageUrls;
            existing.ImageUrl = existing.ImageUrls.FirstOrDefault() ?? existing.ImageUrl;
            existing.LocationCodes = [request.BranchId];

            return OkResponse(ToResponse(existing), "Car updated");
        }
    }

    [HttpPatch("{carId}/active")]
    public IActionResult PatchActive(string carId, PatchActiveRequest request)
    {
        lock (_store.SyncRoot)
        {
            var car = _store.Cars.FirstOrDefault(x => x.Id.Equals(carId, StringComparison.OrdinalIgnoreCase));
            if (car is null)
            {
                return ErrorResponse(StatusCodes.Status404NotFound, "Car not found");
            }

            car.Active = request.Active;
            return OkResponse(new { id = car.Id, active = car.Active }, "Car active status updated");
        }
    }

    [HttpDelete("{carId}")]
    public IActionResult DeleteAdminCar(string carId)
    {
        lock (_store.SyncRoot)
        {
            var car = _store.Cars.FirstOrDefault(x => x.Id.Equals(carId, StringComparison.OrdinalIgnoreCase));
            if (car is null)
            {
                return ErrorResponse(StatusCodes.Status404NotFound, "Car not found");
            }

            _store.Cars.Remove(car);
            return NoContent();
        }
    }

    [HttpPost("{carId}/images")]
    public IActionResult UploadImages(string carId, [FromForm] IFormFileCollection files)
    {
        lock (_store.SyncRoot)
        {
            var car = _store.Cars.FirstOrDefault(x => x.Id.Equals(carId, StringComparison.OrdinalIgnoreCase));
            if (car is null)
            {
                return ErrorResponse(StatusCodes.Status404NotFound, "Car not found");
            }

            var uploaded = new List<object>();
            IEnumerable<IFormFile> safeFiles = files?.Count > 0 ? files : Array.Empty<IFormFile>();

            foreach (var file in safeFiles)
            {
                var fileId = _store.NextId("FIL");
                var extension = Path.GetExtension(file.FileName);
                var url = $"https://cdn.example.com/cars/{car.Id}-{fileId}{extension}";
                car.ImageUrls.Add(url);
                if (string.IsNullOrWhiteSpace(car.ImageUrl))
                {
                    car.ImageUrl = url;
                }

                uploaded.Add(new { fileId, url });
            }

            return OkResponse<IEnumerable<object>>(uploaded, "Images uploaded");
        }
    }

    private IActionResult? ValidateRequest(AdminCarUpsertRequest request)
    {
        var errors = new List<ApiErrorItem>();

        if (!RentXConstants.IsValid(RentXConstants.CarTypes, request.Type))
        {
            errors.Add(new ApiErrorItem { Field = "type", Code = "InvalidCarType", Message = "Invalid car type." });
        }

        if (!RentXConstants.IsValid(RentXConstants.Fuels, request.Fuel))
        {
            errors.Add(new ApiErrorItem { Field = "fuel", Code = "InvalidFuel", Message = "Invalid fuel type." });
        }

        if (!RentXConstants.IsValid(RentXConstants.Transmissions, request.Transmission))
        {
            errors.Add(new ApiErrorItem { Field = "transmission", Code = "InvalidTransmission", Message = "Invalid transmission type." });
        }

        if (!RegNoRegex.IsMatch(request.RegNo ?? string.Empty))
        {
            errors.Add(new ApiErrorItem { Field = "regNo", Code = "InvalidRegNo", Message = "regNo must match pattern like MH12-AB-1234." });
        }

        if (request.Seats < 2 || request.Seats > 8)
        {
            errors.Add(new ApiErrorItem { Field = "seats", Code = "InvalidSeats", Message = "seats must be between 2 and 8." });
        }

        if (request.DailyPrice < 300)
        {
            errors.Add(new ApiErrorItem { Field = "dailyPrice", Code = "InvalidDailyPrice", Message = "dailyPrice must be at least 300." });
        }

        if (request.Odometer < 0)
        {
            errors.Add(new ApiErrorItem { Field = "odometer", Code = "InvalidOdometer", Message = "odometer must be >= 0." });
        }

        if (errors.Count > 0)
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, "Validation failed", errors);
        }

        return null;
    }

    private static object ToResponse(AdminCarRecord x)
    {
        return new
        {
            id = x.Id,
            brand = x.Brand,
            model = x.Model,
            type = x.Type,
            fuel = x.Fuel,
            transmission = x.Transmission,
            seats = x.Seats,
            dailyPrice = x.DailyPrice,
            regNo = x.RegNo,
            odometer = x.Odometer,
            branchId = x.BranchId,
            imageUrls = x.ImageUrls,
            active = x.Active
        };
    }

    public sealed class AdminCarUpsertRequest
    {
        public string Brand { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Fuel { get; set; } = string.Empty;
        public string Transmission { get; set; } = string.Empty;
        public int Seats { get; set; }
        public decimal DailyPrice { get; set; }
        public string RegNo { get; set; } = string.Empty;
        public int Odometer { get; set; }
        public string BranchId { get; set; } = string.Empty;
        public bool Active { get; set; }
        public List<string>? ImageUrls { get; set; }
    }

    public sealed class PatchActiveRequest
    {
        public bool Active { get; set; }
    }
}
