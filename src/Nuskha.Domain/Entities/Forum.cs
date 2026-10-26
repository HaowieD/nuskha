namespace Nuskha.Domain.Entities;

public class ForumCategory
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string? NameEn { get; set; }
    public string? Description { get; set; }
    public string? DescriptionEn { get; set; }
    public int SortOrder { get; set; }
    public List<ForumThread> Threads { get; set; } = [];
}

public class ForumThread
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CategoryId { get; set; }
    public ForumCategory Category { get; set; } = null!;
    /// <summary>URL identifier: /forum/thread/{Slug}</summary>
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public Guid AuthorId { get; set; }
    public AppUser Author { get; set; } = null!;
    public bool IsPinned { get; set; }
    public bool IsLocked { get; set; }
    public int PostCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastPostAt { get; set; } = DateTime.UtcNow;
    public List<ForumPost> Posts { get; set; } = [];
}

public class ForumPost
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ThreadId { get; set; }
    public ForumThread Thread { get; set; } = null!;
    public Guid AuthorId { get; set; }
    public AppUser Author { get; set; } = null!;
    public string Body { get; set; } = "";
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EditedAt { get; set; }
}
