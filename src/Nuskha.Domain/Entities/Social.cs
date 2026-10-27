namespace Nuskha.Domain.Entities;

/// <summary>One user's entry for one book (the "list" in AniList terms).</summary>
public class ListEntry
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public Guid BookId { get; set; }
    public Book Book { get; set; } = null!;
    public ReadingStatus Status { get; set; } = ReadingStatus.PlanToRead;
    /// <summary>1..10, null = not rated.</summary>
    public int? Rating { get; set; }
    public int? PagesRead { get; set; }
    public DateOnly? StartedOn { get; set; }
    public DateOnly? FinishedOn { get; set; }
    public string? Notes { get; set; }
    public bool IsPrivate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class Follow
{
    public Guid FollowerId { get; set; }
    public Guid FolloweeId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Comment
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public TargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public Guid AuthorId { get; set; }
    public AppUser Author { get; set; } = null!;
    /// <summary>Single-level threading: replies point at a top-level comment.</summary>
    public Guid? ParentId { get; set; }
    public string Body { get; set; } = "";
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Like
{
    public Guid UserId { get; set; }
    public TargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ActivityEvent
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public ActivityType Type { get; set; }
    public Guid? BookId { get; set; }
    public Book? Book { get; set; }
    public Guid? ThreadId { get; set; }
    public ForumThread? Thread { get; set; }
    /// <summary>Small payload, e.g. the rating or the new status name.</summary>
    public string? Data { get; set; }
    /// <summary>Markup text of a status post (ActivityType.StatusPost).</summary>
    public string? Body { get; set; }
    /// <summary>Set when a status post is written on someone else's profile.</summary>
    public Guid? RecipientId { get; set; }
    public AppUser? Recipient { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Notification
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public Guid ActorId { get; set; }
    public AppUser Actor { get; set; } = null!;
    public NotificationType Type { get; set; }
    public string Url { get; set; } = "/";
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Report
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ReporterId { get; set; }
    public AppUser Reporter { get; set; } = null!;
    public TargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public string Reason { get; set; } = "";
    public ReportStatus Status { get; set; } = ReportStatus.Open;
    public Guid? HandledById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? HandledAt { get; set; }
}
