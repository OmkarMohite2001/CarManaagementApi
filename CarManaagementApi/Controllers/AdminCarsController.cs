using System.Text.RegularExpressions;
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
[Route("api/v1/admin/cars")]
public class AdminCarsController : ApiControllerBase
{
    private static readonly Regex RegNoRegex = new("^[A-Z]{2}\\d{2}-[A-Z]{2}-\\d{4}$", RegexOptions.Compiled);

    private readonly RentXDbContext _db;

    public AdminCarsController(RentXDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAdminCars(
        [FromQuery] string? q,
        [FromQuery] string? branchId,
        [FromQuery] string? type,
        [FromQuery] string? fuel,
        [FromQuery] string? transmission,
        [FromQuery] bool? active,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = _db.Cars
            .AsNoTracking()
            .Include(x => x.CarImages)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(x =>
                x.CarId.Contains(q)
                || x.Brand.Contains(q)
                || x.Model.Contains(q)
                || x.RegNo.Contains(q));
        }

        if (!string.IsNullOrWhiteSpace(branchId))
        {
            query = query.Where(x => x.BranchId == branchId);
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(x => x.CarType == type);
        }

        if (!string.IsNullOrWhiteSpace(fuel))
        {
            query = query.Where(x => x.Fuel == fuel);
        }

        if (!string.IsNullOrWhiteSpace(transmission))
        {
            query = query.Where(x => x.Transmission == transmission);
        }

        if (active.HasValue)
        {
            query = query.Where(x => x.IsActive == active.Value);
        }

        var rows = await query
            .OrderBy(x => x.Brand)
            .ThenBy(x => x.Model)
            .Select(x => ToResponse(x))
            .ToListAsync();

        var (items, meta) = rows.Paginate(page, pageSize);
        return OkResponse<IEnumerable<object>>(items, meta: meta);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAdminCar(AdminCarUpsertRequest request)
    {
        var validation = ValidateRequest(request);
        if (validation is not null)
        {
            return validation;
        }

        var branchExists = await _db.Branches.AnyAsync(x => x.BranchId == request.BranchId);
        if (!branchExists)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "Branch not found");
        }

        var regExists = await _db.Cars.AnyAsync(x => x.RegNo == request.RegNo);
        if (regExists)
        {
            return ErrorResponse(StatusCodes.Status409Conflict, "Duplicate regNo");
        }

        var carId = await IdGenerator.NextAsync(_db.Cars.Select(x => x.CarId), "CAR");

        var car = new Car
        {
            CarId = carId,
            Brand = request.Brand,
            Model = request.Model,
            CarType = request.Type.ToLowerInvariant(),
            Fuel = request.Fuel.ToLowerInvariant(),
            Transmission = request.Transmission.ToLowerInvariant(),
            Seats = (byte)request.Seats,
            DailyPrice = request.DailyPrice,
            RegNo = request.RegNo.ToUpperInvariant(),
            Odometer = request.Odometer,
            BranchId = request.BranchId,
            IsActive = request.Active,
            Rating = 4.5m,
            PrimaryImageUrl = request.ImageUrls?.FirstOrDefault(),
            CreatedAt = DateTime.UtcNow
        };

        _db.Cars.Add(car);

        if (request.ImageUrls is not null)
        {
            var sort = 1;
            foreach (var imageUrl in request.ImageUrls)
            {
                _db.CarImages.Add(new CarImage
                {
                    CarId = car.CarId,
                    ImageUrl = imageUrl,
                    SortOrder = sort++
                });
            }
        }

        await _db.SaveChangesAsync();

        car.CarImages = await _db.CarImages.Where(x => x.CarId == car.CarId).OrderBy(x => x.SortOrder).ToListAsync();

        return OkResponse(ToResponse(car), "Car created");
    }

    [HttpPut("{carId}")]
    public async Task<IActionResult> UpdateAdminCar(string carId, AdminCarUpsertRequest request)
    {
        var validation = ValidateRequest(request);
        if (validation is not null)
        {
            return validation;
        }

        var existing = await _db.Cars.Include(x => x.CarImages).FirstOrDefaultAsync(x => x.CarId == carId);
        if (existing is null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "Car not found");
        }

        var branchExists = await _db.Branches.AnyAsync(x => x.BranchId == request.BranchId);
        if (!branchExists)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "Branch not found");
        }

        var regExists = await _db.Cars.AnyAsync(x => x.CarId != carId && x.RegNo == request.RegNo);
        if (regExists)
        {
            return ErrorResponse(StatusCodes.Status409Conflict, "Duplicate regNo");
        }

        existing.Brand = request.Brand;
        existing.Model = request.Model;
        existing.CarType = request.Type.ToLowerInvariant();
        existing.Fuel = request.Fuel.ToLowerInvariant();
        existing.Transmission = request.Transmission.ToLowerInvariant();
        existing.Seats = (byte)request.Seats;
        existing.DailyPrice = request.DailyPrice;
        existing.RegNo = request.RegNo.ToUpperInvariant();
        existing.Odometer = request.Odometer;
        existing.BranchId = request.BranchId;
        existing.IsActive = request.Active;
        existing.UpdatedAt = DateTime.UtcNow;

        if (request.ImageUrls is not null)
        {
            _db.CarImages.RemoveRange(existing.CarImages);
            var sort = 1;
            foreach (var imageUrl in request.ImageUrls)
            {
                _db.CarImages.Add(new CarImage
                {
                    CarId = existing.CarId,
                    ImageUrl = imageUrl,
                    SortOrder = sort++
                });
            }
            existing.PrimaryImageUrl = request.ImageUrls.FirstOrDefault();
        }

        await _db.SaveChangesAsync();

        existing.CarImages = await _db.CarImages.Where(x => x.CarId == existing.CarId).OrderBy(x => x.SortOrder).ToListAsync();

        return OkResponse(ToResponse(existing), "Car updated");
    }

    [HttpPatch("{carId}/active")]
    public async Task<IActionResult> PatchActive(string carId, PatchActiveRequest request)
    {
        var car = await _db.Cars.FirstOrDefaultAsync(x => x.CarId == carId);
        if (car is null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "Car not found");
        }

        car.IsActive = request.Active;
        car.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return OkResponse(new { id = car.CarId, active = car.IsActive }, "Car active status updated");
    }

    [HttpDelete("{carId}")]
    public async Task<IActionResult> DeleteAdminCar(string carId)
    {
        var car = await _db.Cars.FirstOrDefaultAsync(x => x.CarId == carId);
        if (car is null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "Car not found");
        }

        _db.Cars.Remove(car);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{carId}/images")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadImages(string carId, IFormFileCollection files)
    {
        var car = await _db.Cars.FirstOrDefaultAsync(x => x.CarId == carId);
        if (car is null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "Car not found");
        }

        var maxSort = await _db.CarImages.Where(x => x.CarId == carId).Select(x => (int?)x.SortOrder).MaxAsync() ?? 0;
        var uploaded = new List<object>();
        IEnumerable<IFormFile> safeFiles = files?.Count > 0 ? files : Array.Empty<IFormFile>();

        foreach (var file in safeFiles)
        {
            var fileId = await IdGenerator.NextAsync(_db.CarImages.Select(x => "FIL-" + x.CarImageId), "FIL");
            var extension = Path.GetExtension(file.FileName);
            var url = "https://cdn.example.com/cars/" + car.CarId + "-" + fileId + extension;

            maxSort++;
            _db.CarImages.Add(new CarImage
            {
                CarId = car.CarId,
                ImageUrl = url,
                SortOrder = maxSort
            });

            if (string.IsNullOrWhiteSpace(car.PrimaryImageUrl))
            {
                car.PrimaryImageUrl = url;
            }

            uploaded.Add(new { fileId, url });
        }

        await _db.SaveChangesAsync();

        return OkResponse<IEnumerable<object>>(uploaded, "Images uploaded");
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

    private static object ToResponse(Car x)
    {
        return new
        {
            id = x.CarId,
            brand = x.Brand,
            model = x.Model,
            type = x.CarType,
            fuel = x.Fuel,
            transmission = x.Transmission,
            seats = x.Seats,
            dailyPrice = x.DailyPrice,
            regNo = x.RegNo,
            odometer = x.Odometer,
            branchId = x.BranchId,
            imageUrls = x.CarImages.OrderBy(i => i.SortOrder).Select(i => i.ImageUrl).ToList(),
            active = x.IsActive
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
