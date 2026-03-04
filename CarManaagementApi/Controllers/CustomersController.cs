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
[Route("api/v1/customers")]
public class CustomersController : ApiControllerBase
{
    private static readonly Regex PhoneRegex = new("^[6-9]\\d{9}$", RegexOptions.Compiled);
    private static readonly Regex PincodeRegex = new("^\\d{6}$", RegexOptions.Compiled);

    private readonly IRentXStore _store;

    public CustomersController(IRentXStore store)
    {
        _store = store;
    }

    [HttpGet]
    public IActionResult GetCustomers(
        [FromQuery] string? q,
        [FromQuery] string? type,
        [FromQuery] string? city,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        lock (_store.SyncRoot)
        {
            var query = _store.Customers.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                query = query.Where(x =>
                    x.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || x.Email.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || x.Phone.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || x.Id.Contains(q, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(type))
            {
                query = query.Where(x => x.Type.Equals(type, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(city))
            {
                query = query.Where(x => x.City.Equals(city, StringComparison.OrdinalIgnoreCase));
            }

            var shaped = query
                .OrderBy(x => x.Name)
                .Select(x => (object)new
                {
                    id = x.Id,
                    type = x.Type,
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
                });

            var (items, meta) = shaped.Paginate(page, pageSize);
            return OkResponse<IEnumerable<object>>(items, meta: meta);
        }
    }

    [HttpPost]
    public IActionResult CreateCustomer(CustomerUpsertRequest request)
    {
        var validation = ValidateCustomer(request);
        if (validation is not null)
        {
            return validation;
        }

        lock (_store.SyncRoot)
        {
            if (_store.Customers.Any(x => x.Email.Equals(request.Email, StringComparison.OrdinalIgnoreCase)))
            {
                return ErrorResponse(StatusCodes.Status409Conflict, "Customer email already exists");
            }

            var customer = MapToCustomer(_store.NextId("CUS"), request);
            _store.Customers.Add(customer);

            return OkResponse(ToCustomerResponse(customer), "Customer created");
        }
    }

    [HttpGet("{customerId}")]
    public IActionResult GetCustomerById(string customerId)
    {
        lock (_store.SyncRoot)
        {
            var customer = _store.Customers.FirstOrDefault(x => x.Id.Equals(customerId, StringComparison.OrdinalIgnoreCase));
            if (customer is null)
            {
                return ErrorResponse(StatusCodes.Status404NotFound, "Customer not found");
            }

            return OkResponse(ToCustomerResponse(customer));
        }
    }

    [HttpPut("{customerId}")]
    public IActionResult UpdateCustomer(string customerId, CustomerUpsertRequest request)
    {
        var validation = ValidateCustomer(request);
        if (validation is not null)
        {
            return validation;
        }

        lock (_store.SyncRoot)
        {
            var existing = _store.Customers.FirstOrDefault(x => x.Id.Equals(customerId, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                return ErrorResponse(StatusCodes.Status404NotFound, "Customer not found");
            }

            if (_store.Customers.Any(x =>
                    !x.Id.Equals(customerId, StringComparison.OrdinalIgnoreCase)
                    && x.Email.Equals(request.Email, StringComparison.OrdinalIgnoreCase)))
            {
                return ErrorResponse(StatusCodes.Status409Conflict, "Customer email already exists");
            }

            existing.Type = request.Type;
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

            return OkResponse(ToCustomerResponse(existing), "Customer updated");
        }
    }

    [HttpDelete("{customerId}")]
    public IActionResult DeleteCustomer(string customerId)
    {
        lock (_store.SyncRoot)
        {
            var customer = _store.Customers.FirstOrDefault(x => x.Id.Equals(customerId, StringComparison.OrdinalIgnoreCase));
            if (customer is null)
            {
                return ErrorResponse(StatusCodes.Status404NotFound, "Customer not found");
            }

            _store.Customers.Remove(customer);
            return NoContent();
        }
    }

    private IActionResult? ValidateCustomer(CustomerUpsertRequest request)
    {
        var errors = new List<ApiErrorItem>();

        if (!PhoneRegex.IsMatch(request.Phone))
        {
            errors.Add(new ApiErrorItem { Field = "phone", Code = "InvalidPhone", Message = "Phone must match ^[6-9]\\d{9}$." });
        }

        if (!PincodeRegex.IsMatch(request.Pincode))
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

    private static CustomerRecord MapToCustomer(string id, CustomerUpsertRequest request)
    {
        return new CustomerRecord
        {
            Id = id,
            Type = request.Type,
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
            Pincode = request.Pincode
        };
    }

    private static object ToCustomerResponse(CustomerRecord x)
    {
        return new
        {
            id = x.Id,
            type = x.Type,
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
        public string Email { get; set; } = string.Empty;
        public DateOnly? Dob { get; set; }
        public string KycType { get; set; } = string.Empty;
        public string KycNumber { get; set; } = string.Empty;
        public string? DlNumber { get; set; }
        public DateOnly? DlExpiry { get; set; }
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Pincode { get; set; } = string.Empty;
    }
}
