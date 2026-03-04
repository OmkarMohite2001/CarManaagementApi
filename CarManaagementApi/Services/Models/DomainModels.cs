namespace CarManaagementApi.Services.Models;

public sealed class BranchRecord
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Pincode { get; set; } = string.Empty;
    public TimeOnly OpenAt { get; set; }
    public TimeOnly CloseAt { get; set; }
    public bool Active { get; set; }
}

public sealed class AdminCarRecord
{
    public string Id { get; set; } = string.Empty;
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
    public decimal Rating { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public List<string> ImageUrls { get; set; } = [];
    public List<string> LocationCodes { get; set; } = [];
}

public sealed class CustomerRecord
{
    public string Id { get; set; } = string.Empty;
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

public sealed class BookingRecord
{
    public string Id { get; set; } = string.Empty;
    public DateTime PickAt { get; set; }
    public DateTime DropAt { get; set; }
    public string LocationCode { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CarId { get; set; } = string.Empty;
    public string CarName { get; set; } = string.Empty;
    public string CarType { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public int Days { get; set; }
    public decimal DailyPrice { get; set; }
    public string? CancelReason { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class MaintenanceBlockRecord
{
    public string Id { get; set; } = string.Empty;
    public string CarId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public int Days { get; set; }
    public string? Notes { get; set; }
}

public sealed class DamageRecord
{
    public string Part { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public decimal EstCost { get; set; }
    public string? Notes { get; set; }
    public List<string> PhotoUrls { get; set; } = [];
}

public sealed class ReturnInspectionRecord
{
    public string Id { get; set; } = string.Empty;
    public string BookingId { get; set; } = string.Empty;
    public string CarId { get; set; } = string.Empty;
    public int Odometer { get; set; }
    public int FuelPercent { get; set; }
    public bool CleaningRequired { get; set; }
    public int LateHours { get; set; }
    public decimal LateFeePerHour { get; set; }
    public decimal Deposit { get; set; }
    public string? Notes { get; set; }
    public List<DamageRecord> Damages { get; set; } = [];
    public decimal TotalDamage { get; set; }
    public decimal FuelCharge { get; set; }
    public decimal CleaningCharge { get; set; }
    public decimal LateFee { get; set; }
    public decimal SubTotal { get; set; }
    public decimal NetPayable { get; set; }
    public decimal Refund { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class UserRecord
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool Active { get; set; } = true;
    public string Password { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLogin { get; set; }
}

public sealed class ProfileRecord
{
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Gender { get; set; } = "male";
    public DateOnly? Dob { get; set; }
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Pincode { get; set; } = string.Empty;
    public bool NotifEmail { get; set; }
    public bool NotifSms { get; set; }
    public bool NotifWhatsApp { get; set; }
    public string AvatarUrl { get; set; } = string.Empty;
}

public sealed class NotificationRecord
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class RefreshTokenRecord
{
    public string Token { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool Revoked { get; set; }
}

public sealed class PermissionRecord
{
    public bool View { get; set; }
    public bool Create { get; set; }
    public bool Edit { get; set; }
    public bool Delete { get; set; }
    public bool Approve { get; set; }
}
