using Microsoft.AspNetCore.Identity;

namespace Nuskha.Domain.Entities;

public class AppUser : IdentityUser<Guid>
{
    public AppUser() { Id = Guid.CreateVersion7(); }

    public string DisplayName { get; set; } = "";
    public string? Bio { get; set; }
    public string? AvatarPath { get; set; }
    /// <summary>Wide image shown at the top of the profile page.</summary>
    public string? BannerPath { get; set; }
    public bool ListsArePublic { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
}

public class AppRole : IdentityRole<Guid>
{
    public AppRole() { }
    public AppRole(string name) : base(name) { Id = Guid.CreateVersion7(); }
}
