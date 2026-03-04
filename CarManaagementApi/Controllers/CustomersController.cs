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
[Route("api/v1/customers")]
public class CustomersController : ApiControllerBase
{
    private static readonly Regex PhoneRegex = new("^[6-9]\\d{9}$", RegexOptions.Compiled);
    private static readonly Regex PincodeRegex = new("^\\d{6}$", RegexOptions.Compiled);

    private readonly RentXDbContext _db;

    public CustomersController(RentXDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetCustomers(
        [FromQuery] string? q,
        [FromQuery] string? type,
        [FromQuery] string? city,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = _db.Customers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(x =>
                x.Name.Contains(q)
                || (x.Email != null && x.Email.Contains(q))
                || x.Phone.Contains(q)
                || x.CustomerId.Contains(q));
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(x => x.CustomerType == type);
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            query = query.Where(x => x.City == city);
        }

        var shaped = await query
            .OrderBy(x => x.Name)
            .Select(x => (object)new
            {
                id = x.CustomerId,
                type = x.CustomerType,
                name = x.Name,
                phone = x.Phone,
                email = x.Email,
                dob = x.Dob,
                kycType = x.KycType,
                kycNumber = x.KycNumber,
                dlNumber = x.DlNumber,
                dlExpiry = x.DlExpiry,
                address = x.Address,
                city = x.City,
                state = x.State,
                pincode = x.Pincode
            })
            .ToListAsync();

        var (items, meta) = shaped.Paginate(page, pageSize);
        return OkResponse<IEnumerable<object>>(items, meta: meta);
    }

    [HttpPost]
    public async Task<IActionResult> CreateCustomer(CustomerUpsertRequest request)
    {
        var validation = ValidateCustomer(request);
        if (validation is not null)
        {
            return validation;
        }

        var emailExists = !string.IsNullOrWhiteSpace(request.Email)
            && await _db.Customers.AnyAsync(x => x.Email == request.Email);

        if (emailExists)
        {
            return ErrorResponse(StatusCodes.Status409Conflict, "Customer email already exists");
        }

        var customerId = await IdGenerator.NextAsync(_db.Customers.Select(x => x.CustomerId), "CUS");

        var customer = MapToCustomer(customerId, request);
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        return OkResponse(ToCustomerResponse(customer), "Customer created");
    }

    [HttpGet("{customerId}")]
    public async Task<IActionResult> GetCustomerById(string customerId)
    {
        var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.CustomerId == customerId);
        if (customer is null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "Customer not found");
        }

        return OkResponse(ToCustomerResponse(customer));
    }

    [HttpPut("{customerId}")]
    public async Task<IActionResult> UpdateCustomer(string customerId, CustomerUpsertRequest request)
    {
        var validation = ValidateCustomer(request);
        if (validation is not null)
        {
            return validation;
        }

        var existing = await _db.Customers.FirstOrDefaultAsync(x => x.CustomerId == customerId);
        if (existing is null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "Customer not found");
        }

        var emailExists = !string.IsNullOrWhiteSpace(request.Email)
            && await _db.Customers.AnyAsync(x => x.CustomerId != customerId && x.Email == request.Email);

        if (emailExists)
        {
            return ErrorResponse(StatusCodes.Status409Conflict, "Customer email already exists");
        }

        existing.CustomerType = request.Type;
        existing.Name = request.Name;
        existing.Phone = request.Phone;
        existing.Email = request.Email;
        existing.Dob = request.Dob;
        existing.KycType = request.KycType;
        existing.KycNumber = request.KycNumber;
        existing.DlNumber = request.DlNumber;
        existing.DlExpiry = request.DlExpiry;
        existing.Address = request.Address;
        existing.City = request.City;
        existing.State = request.State;
        existing.Pincode = request.Pincode;
        existing.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return OkResponse(ToCustomerResponse(existing), "Customer updated");
    }

    [HttpDelete("{customerId}")]
    public async Task<IActionResult> DeleteCustomer(string customerId)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(x => x.CustomerId == customerId);
        if (customer is null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "Customer not found");
        }

        _db.Customers.Remove(customer);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private IActionResult? ValidateCustomer(CustomerUpsertRequest request)
    {
        var errors = new List<ApiErrorItem>();

        if (!PhoneRegex.IsMatch(request.Phone))
        {
            errors.Add(new ApiErrorItem { Field = "phone", Code = "InvalidPhone", Message = "Phone must match ^[6-9]\\d{9}$." });
        }

        if (!PincodeRegex.IsMatch(request.Pincode ?? string.Empty))
        {
            errors.Add(new ApiErrorItem { Field = "pincode", Code = "InvalidPincode", Message = "Pincode must be 6 digits." });
        }

        if (!RentXConstants.IsValid(RentXConstants.KycTypes, request.KycType))
        {
            errors.Add(new ApiErrorItem { Field = "kycType", Code = "InvalidKycType", Message = "Invalid kycType." });
        }

        if (request.Dob.HasValue)
        {
            var age = DateOnly.FromDateTime(DateTime.UtcNow).Year - request.Dob.Value.Year;
            if (request.Dob.Value > DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-age))
            {
                age--;
            }

            if (age < 18)
            {
                errors.Add(new ApiErrorItem { Field = "dob", Code = "UnderAge", Message = "Customer age must be at least 18 years." });
            }
        }

        if (request.DlExpiry.HasValue && request.DlExpiry.Value <= DateOnly.FromDateTime(DateTime.UtcNow))
        {
            errors.Add(new ApiErrorItem { Field = "dlExpiry", Code = "InvalidDlExpiry", Message = "dlExpiry must be a future date." });
        }

        if (errors.Count > 0)
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, "Validation failed", errors);
        }

        return null;
    }

    private static Customer MapToCustomer(string id, CustomerUpsertRequest request)
    {
        return new Customer
        {
            CustomerId = id,
            CustomerType = request.Type,
            Name = request.Name,
            Phone = request.Phone,
            Email = request.Email,
            Dob = request.Dob,
            KycType = request.KycType,
            KycNumber = request.KycNumber,
            DlNumber = request.DlNumber,
            DlExpiry = request.DlExpiry,
            Address = request.Address,
            City = request.City,
            State = request.State,
            Pincode = request.Pincode,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static object ToCustomerResponse(Customer x)
    {
        return new
        {
            id = x.CustomerId,
            type = x.CustomerType,
            name = x.Name,
            phone = x.Phone,
            email = x.Email,
            dob = x.Dob,
            kycType = x.KycType,
            kycNumber = x.KycNumber,
            dlNumber = x.DlNumber,
            dlExpiry = x.DlExpiry,
            address = x.Address,
            city = x.City,
            state = x.State,
            pincode = x.Pincode
        };
    }

    public sealed class CustomerUpsertRequest
    {
        public string Type { get; set; } = "individual";
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? Email { get; set; }
        public DateOnly? Dob { get; set; }
        public string KycType { get; set; } = string.Empty;
        public string KycNumber { get; set; } = string.Empty;
        public string? DlNumber { get; set; }
        public DateOnly? DlExpiry { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Pincode { get; set; }
    }
}
