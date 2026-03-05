using System.Collections.Generic;

namespace CarManaagementApi.Persistence.Entities;

public partial class User
{
    public bool IsEmailVerified { get; set; }

    public virtual ICollection<UserEmailVerification> UserEmailVerifications { get; set; } = new List<UserEmailVerification>();
}
