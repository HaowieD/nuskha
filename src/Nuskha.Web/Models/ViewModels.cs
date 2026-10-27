using Nuskha.Domain;
using Nuskha.Infrastructure.Services;

namespace Nuskha.Web.Models;

public record AuthorRef(string Slug, string Name, string? NameEn);

public class BookListItem
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string? TitleEn { get; set; }
    public string? CoverPath { get; set; }
    public int? PublishYear { get; set; }
    public int ListCount { get; set; }
    public int RatingCount { get; set; }
    public int RatingSum { get; set; }
    public List<AuthorRef> Authors { get; set; } = [];
    public double? Average => RatingCount == 0 ? null : (double)RatingSum / RatingCount;
}

public record AvatarModel(string? Path, string Name, string Size);

public record LikeButtonModel(TargetType Type, Guid Id, int Count, bool Liked);

public record FollowButtonModel(Guid UserId, bool Following, int FollowerCount);

public class CommentThreadModel
{
    public TargetType Type { get; set; }
    public Guid TargetId { get; set; }
    public List<CommentVm> Comments { get; set; } = [];
    public Guid? ViewerId { get; set; }
    public bool IsModerator { get; set; }
    public string DomId => $"comments-{(int)Type}-{TargetId}";
}

public record CommentItemModel(CommentThreadModel Thread, CommentVm Comment, bool IsReply);

public class FeedPageModel
{
    public List<ActivityItem> Items { get; set; } = [];
    public string? NextUrl { get; set; }
}

public record PagerModel(int Page, int TotalPages);

public class ListEditorModel
{
    public Guid BookId { get; set; }
    public string Slug { get; set; } = "";
    public bool Exists { get; set; }
    public ReadingStatus Status { get; set; } = ReadingStatus.PlanToRead;
    public int? Rating { get; set; }
    public int? PagesRead { get; set; }
    public DateOnly? StartedOn { get; set; }
    public DateOnly? FinishedOn { get; set; }
    public string? Notes { get; set; }
    public bool IsPrivate { get; set; }
    public bool Saved { get; set; }
}

public record FieldErrorModel(Dictionary<string, string> Errors, string Field);

public record AuthorListItem(string Slug, string Name, string? NameEn, int BookCount);

/// <summary>A markup textarea with a formatting toolbar and preview. <see cref="Name"/> is the posted field name.</summary>
public record MdEditorModel(string Id, string Name, string? Value, int Rows = 4, int MaxLength = 2000, bool Required = false, string? Placeholder = null);

/// <summary>Posted fields shared by the reader submission form and the admin create/edit forms (bound as "Form.*").</summary>
public class BookForm
{
    public string? Title { get; set; }
    public string? TitleEn { get; set; }
    /// <summary>One author per line.</summary>
    public string? Authors { get; set; }
    public string? Isbn { get; set; }
    public string? Publisher { get; set; }
    public int? PublishYear { get; set; }
    public int? PageCount { get; set; }
    public string? Language { get; set; }
    public string? Description { get; set; }
    public List<Guid> GenreIds { get; set; } = [];
    /// <summary>Comma-separated tag names.</summary>
    public string? Tags { get; set; }
    public BookStatus Status { get; set; } = BookStatus.Published;
    public bool RemoveCover { get; set; }
    public Microsoft.AspNetCore.Http.IFormFile? Cover { get; set; }
}

/// <param name="StatusOptions">Statuses a staff member may choose between; null hides the status picker (reader form).</param>
public record BookFormView(BookForm Form, Dictionary<string, string> Errors, List<Nuskha.Domain.Entities.Genre> Genres,
    bool ShowLanguage, IReadOnlyList<BookStatus>? StatusOptions, string? CoverPath);

/// <param name="To">User name of the profile being written on, or null for your own feed.</param>
public record StatusComposerModel(string? To, string Label, string? Placeholder = null);
