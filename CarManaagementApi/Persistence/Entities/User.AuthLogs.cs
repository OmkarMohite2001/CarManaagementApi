using System.Collections.Generic;

namespace CarManaagementApi.Persistence.Entities;

public partial class User
{
    public virtual ICollection<UserAuthLog> UserAuthLogs { get; set; } = new List<UserAuthLog>();
}
