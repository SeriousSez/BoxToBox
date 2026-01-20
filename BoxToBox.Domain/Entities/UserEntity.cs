using Microsoft.AspNetCore.Identity;

namespace BoxToBox.Domain.Entities;

public class UserEntity : IdentityUser<Guid>
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public override string? Email { get; set; } = string.Empty;
    public DateTime Created { get; set; }
    public DateTime Modified { get; set; }
}
