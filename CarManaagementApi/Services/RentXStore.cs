using CarManaagementApi.Services.Models;

namespace CarManaagementApi.Services;

public interface IRentXStore
{
    object SyncRoot { get; }
    List<BranchRecord> Branches { get; }
    List<AdminCarRecord> Cars { get; }
    List<CustomerRecord> Customers { get; }
    List<BookingRecord> Bookings { get; }
    List<MaintenanceBlockRecord> MaintenanceBlocks { get; }
    List<ReturnInspectionRecord> ReturnInspections { get; }
    List<UserRecord> Users { get; }
    Dictionary<string, ProfileRecord> ProfilesByUserId { get; }
    List<NotificationRecord> Notifications { get; }
    List<RefreshTokenRecord> RefreshTokens { get; }
    Dictionary<string, Dictionary<string, PermissionRecord>> RolePermissions { get; }
    string NextId(string prefix);
}

public sealed class RentXStore : IRentXStore
{
    private readonly Dictionary<string, int> _sequences = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CAR"] = 1004,
        ["CUS"] = 1003,
        ["BK"] = 1048,
        ["MT"] = 1004,
        ["INSP"] = 1002,
        ["U"] = 1006,
        ["NOT"] = 1005,
        ["FIL"] = 1002
    };

    public RentXStore()
    {
        var now = DateTime.UtcNow;

        Branches =
        [
            new BranchRecord
            {
                Id = "PNQ",
                Name = "Pune",
                Phone = "020-11112222",
                Email = "pune@company.com",
                Address = "Airport Road",
                City = "Pune",
                State = "MH",
                Pincode = "411001",
                OpenAt = new TimeOnly(9, 0),
                CloseAt = new TimeOnly(21, 0),
                Active = true
            },
            new BranchRecord
            {
                Id = "BOM",
                Name = "Mumbai",
                Phone = "022-11112222",
                Email = "mumbai@company.com",
                Address = "Andheri East",
                City = "Mumbai",
                State = "MH",
                Pincode = "400069",
                OpenAt = new TimeOnly(9, 0),
                CloseAt = new TimeOnly(21, 0),
                Active = true
            },
            new BranchRecord
            {
                Id = "NAG",
                Name = "Nagpur",
                Phone = "0712-11112222",
                Email = "nagpur@company.com",
                Address = "Wardha Road",
                City = "Nagpur",
                State = "MH",
                Pincode = "440015",
                OpenAt = new TimeOnly(9, 0),
                CloseAt = new TimeOnly(21, 0),
                Active = true
            }
        ];

        Cars =
        [
            new AdminCarRecord
            {
                Id = "CAR-1001",
                Brand = "Honda",
                Model = "City",
                Type = "sedan",
                Fuel = "petrol",
                Transmission = "automatic",
                Seats = 5,
                DailyPrice = 2700,
                RegNo = "MH12-AB-1234",
                Odometer = 42000,
                BranchId = "PNQ",
                Active = true,
                Rating = 4.6m,
                ImageUrl = "https://cdn.example.com/cars/city.jpg",
                ImageUrls = ["https://cdn.example.com/cars/city-1.jpg"],
                LocationCodes = ["PNQ", "BOM"]
            },
            new AdminCarRecord
            {
                Id = "CAR-1002",
                Brand = "Hyundai",
                Model = "Creta",
                Type = "suv",
                Fuel = "diesel",
                Transmission = "manual",
                Seats = 5,
                DailyPrice = 3200,
                RegNo = "MH01-CD-8899",
                Odometer = 30000,
                BranchId = "BOM",
                Active = true,
                Rating = 4.5m,
                ImageUrl = "https://cdn.example.com/cars/creta.jpg",
                ImageUrls = ["https://cdn.example.com/cars/creta-1.jpg"],
                LocationCodes = ["BOM"]
            },
            new AdminCarRecord
            {
                Id = "CAR-1003",
                Brand = "Tata",
                Model = "Nexon EV",
                Type = "suv",
                Fuel = "ev",
                Transmission = "automatic",
                Seats = 5,
                DailyPrice = 3500,
                RegNo = "MH31-EV-2233",
                Odometer = 12000,
                BranchId = "NAG",
                Active = true,
                Rating = 4.7m,
                ImageUrl = "https://cdn.example.com/cars/nexon-ev.jpg",
                ImageUrls = ["https://cdn.example.com/cars/nexon-ev-1.jpg"],
                LocationCodes = ["NAG", "PNQ"]
            }
        ];

        Customers =
        [
            new CustomerRecord
            {
                Id = "CUS-1001",
                Type = "individual",
                Name = "A. Kulkarni",
                Phone = "9876543210",
                Email = "akulkarni@example.com",
                Dob = new DateOnly(1994, 8, 21),
                KycType = "aadhaar",
                KycNumber = "123412341234",
                DlNumber = "DL0420110149646",
                DlExpiry = new DateOnly(2028, 12, 31),
                Address = "Koregaon Park",
                City = "Pune",
                State = "MH",
                Pincode = "411001"
            },
            new CustomerRecord
            {
                Id = "CUS-1002",
                Type = "individual",
                Name = "Rahul Sharma",
                Phone = "9988776655",
                Email = "rahul@example.com",
                Dob = new DateOnly(1992, 1, 10),
                KycType = "pan",
                KycNumber = "ABCDE1234F",
                DlNumber = "DL0420151234567",
                DlExpiry = new DateOnly(2029, 1, 1),
                Address = "Baner",
                City = "Pune",
                State = "MH",
                Pincode = "411045"
            }
        ];

        Bookings =
        [
            new BookingRecord
            {
                Id = "BK-1045",
                PickAt = now.Date.AddDays(1).AddHours(10),
                DropAt = now.Date.AddDays(3).AddHours(10),
                LocationCode = "PNQ",
                CustomerId = "CUS-1001",
                CustomerName = "A. Kulkarni",
                CarId = "CAR-1001",
                CarName = "Honda City",
                CarType = "sedan",
                Status = "pending",
                Days = 2,
                DailyPrice = 2700,
                CreatedAt = now.AddHours(-4)
            },
            new BookingRecord
            {
                Id = "BK-1046",
                PickAt = now.Date.AddHours(9),
                DropAt = now.Date.AddDays(2).AddHours(9),
                LocationCode = "BOM",
                CustomerId = "CUS-1002",
                CustomerName = "Rahul Sharma",
                CarId = "CAR-1002",
                CarName = "Hyundai Creta",
                CarType = "suv",
                Status = "ongoing",
                Days = 2,
                DailyPrice = 3200,
                CreatedAt = now.AddDays(-1)
            },
            new BookingRecord
            {
                Id = "BK-1047",
                PickAt = now.Date.AddDays(-2).AddHours(8),
                DropAt = now.Date.AddDays(-1).AddHours(8),
                LocationCode = "NAG",
                CustomerId = "CUS-1001",
                CustomerName = "A. Kulkarni",
                CarId = "CAR-1003",
                CarName = "Tata Nexon EV",
                CarType = "suv",
                Status = "completed",
                Days = 1,
                DailyPrice = 3500,
                CreatedAt = now.AddDays(-4)
            }
        ];

        MaintenanceBlocks =
        [
            new MaintenanceBlockRecord
            {
                Id = "MT-1003",
                CarId = "CAR-1001",
                Type = "service",
                From = DateOnly.FromDateTime(now.Date.AddDays(5)),
                To = DateOnly.FromDateTime(now.Date.AddDays(7)),
                Days = 3,
                Notes = "60k km service"
            }
        ];

        ReturnInspections = [];

        Users =
        [
            new UserRecord
            {
                Id = "U-1004",
                Name = "Admin User",
                Username = "admin",
                Email = "admin@demo.com",
                Phone = "9876543210",
                Role = "admin",
                Active = true,
                Password = "admin",
                CreatedAt = now.AddMonths(-6),
                LastLogin = now.AddMinutes(-10)
            },
            new UserRecord
            {
                Id = "U-1005",
                Name = "Ops Lead",
                Username = "opslead",
                Email = "ops@company.com",
                Phone = "9765432109",
                Role = "ops",
                Active = true,
                Password = "Temp@123",
                CreatedAt = now.AddMonths(-3),
                LastLogin = now.AddHours(-6)
            }
        ];

        ProfilesByUserId = new Dictionary<string, ProfileRecord>(StringComparer.OrdinalIgnoreCase)
        {
            ["U-1004"] = new ProfileRecord
            {
                FullName = "Admin User",
                Username = "admin",
                Email = "admin@demo.com",
                Phone = "9876543210",
                Gender = "male",
                Dob = new DateOnly(1992, 1, 10),
                Address = "Baner",
                City = "Pune",
                State = "MH",
                Pincode = "411045",
                NotifEmail = true,
                NotifSms = false,
                NotifWhatsApp = false,
                AvatarUrl = "https://cdn.example.com/avatar/U-1004.png"
            }
        };

        Notifications =
        [
            new NotificationRecord
            {
                Id = "NOT-1001",
                Title = "Booking pending approval",
                Message = "BK-1045 is waiting for approval",
                IsRead = false,
                CreatedAt = now.AddMinutes(-20)
            },
            new NotificationRecord
            {
                Id = "NOT-1002",
                Title = "Maintenance due",
                Message = "CAR-1001 has a service block this week",
                IsRead = false,
                CreatedAt = now.AddHours(-2)
            },
            new NotificationRecord
            {
                Id = "NOT-1003",
                Title = "Trip completed",
                Message = "BK-1047 was completed",
                IsRead = true,
                CreatedAt = now.AddDays(-1)
            }
        ];

        RefreshTokens = [];

        RolePermissions = CreateDefaultRolePermissions();
    }

    public object SyncRoot { get; } = new();
    public List<BranchRecord> Branches { get; }
    public List<AdminCarRecord> Cars { get; }
    public List<CustomerRecord> Customers { get; }
    public List<BookingRecord> Bookings { get; }
    public List<MaintenanceBlockRecord> MaintenanceBlocks { get; }
    public List<ReturnInspectionRecord> ReturnInspections { get; }
    public List<UserRecord> Users { get; }
    public Dictionary<string, ProfileRecord> ProfilesByUserId { get; }
    public List<NotificationRecord> Notifications { get; }
    public List<RefreshTokenRecord> RefreshTokens { get; }
    public Dictionary<string, Dictionary<string, PermissionRecord>> RolePermissions { get; }

    public string NextId(string prefix)
    {
        lock (SyncRoot)
        {
            if (!_sequences.TryGetValue(prefix, out var next))
            {
                next = 1001;
            }

            _sequences[prefix] = next + 1;
            return $"{prefix.ToUpperInvariant()}-{next}";
        }
    }

    private static Dictionary<string, Dictionary<string, PermissionRecord>> CreateDefaultRolePermissions()
    {
        var modules = new[] { "Bookings", "Cars", "Customers", "Branches", "Maintenance", "Reports" };

        var map = new Dictionary<string, Dictionary<string, PermissionRecord>>(StringComparer.OrdinalIgnoreCase);

        foreach (var role in new[] { "admin", "ops", "agent", "viewer" })
        {
            var permissions = new Dictionary<string, PermissionRecord>(StringComparer.OrdinalIgnoreCase);
            foreach (var module in modules)
            {
                permissions[module] = role switch
                {
                    "admin" => new PermissionRecord { View = true, Create = true, Edit = true, Delete = true, Approve = true },
                    "ops" => new PermissionRecord { View = true, Create = true, Edit = true, Delete = false, Approve = true },
                    "agent" => new PermissionRecord { View = true, Create = true, Edit = false, Delete = false, Approve = false },
                    _ => new PermissionRecord { View = true, Create = false, Edit = false, Delete = false, Approve = false }
                };
            }

            map[role] = permissions;
        }

        return map;
    }
}
