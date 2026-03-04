namespace CarManaagementApi.Contracts;

public static class RentXConstants
{
    public static readonly string[] CarTypes = ["hatchback", "sedan", "suv", "luxury"];
    public static readonly string[] Transmissions = ["manual", "automatic"];
    public static readonly string[] Fuels = ["petrol", "diesel", "ev", "hybrid"];
    public static readonly string[] BookingStatuses = ["pending", "approved", "ongoing", "completed", "cancelled"];
    public static readonly string[] MaintenanceTypes = ["service", "repair", "insurance", "puc", "other"];
    public static readonly string[] DamageSeverities = ["minor", "moderate", "major"];
    public static readonly string[] Roles = ["admin", "ops", "agent", "viewer"];
    public static readonly string[] KycTypes = ["aadhaar", "passport", "pan", "dl"];

    public static bool IsValid(string[] allowed, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return allowed.Contains(value, StringComparer.OrdinalIgnoreCase);
    }
}
