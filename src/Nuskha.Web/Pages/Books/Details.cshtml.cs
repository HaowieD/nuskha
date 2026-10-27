namespace Nuskha.Web.Pages.Books;

public class DetailsModel(NuskhaDbContext db, ListService lists, SocialService social, CommentQueryService comments) : AppPageModel
{
    public record ReaderVm(string UserName, string DisplayName, string? AvatarPath, ReadingStatus Status, int? Rating);

    public Book Book { get; private set; } = null!;
    public ListEditorModel Editor { get; private set; } = null!;
    public LikeState Likes { get; private set; } = new(false, 0);
    public CommentThreadModel Thread { get; private set; } = null!;
    public List<ReaderVm> Readers { get; private set; } = [];
    public List<Genre> Genres { get; private set; } = [];
    public List<Tag> Tags { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken ct)
    {
        var book = await db.Books.AsNoTracking()
            .Include(b => b.Authors.OrderBy(a => a.SortOrder)).ThenInclude(a => a.Author)
            .FirstOrDefaultAsync(b => b.Slug == slug, ct);
        if (book is null || (book.Status != BookStatus.Published && !User.IsStaff())) return NotFound();
        Book = book;

        var me = CurrentUserId;
        var entry = me is Guid uid ? await lists.GetAsync(uid, book.Id, ct) : null;
        Editor = entry is null
            ? new ListEditorModel { BookId = book.Id, Slug = book.Slug }
            : EditorFrom(entry, book.Slug, saved: false);

        Genres = await db.BookGenres.AsNoTracking().Where(x => x.BookId == book.Id).Select(x => x.Genre)
            .OrderBy(g => g.SortOrder).ToListAsync(ct);
        Tags = await db.BookTags.AsNoTracking().Where(x => x.BookId == book.Id).Select(x => x.Tag).OrderBy(t => t.Name).ToListAsync(ct);
        Likes = await social.GetLikeStateAsync(me, TargetType.Book, book.Id, ct);
        Readers = await db.ListEntries.AsNoTracking()
            .Where(e => e.BookId == book.Id && !e.IsPrivate && e.User.ListsArePublic)
            .OrderByDescending(e => e.UpdatedAt).Take(12)
            .Select(e => new ReaderVm(e.User.UserName!, e.User.DisplayName, e.User.AvatarPath, e.Status, e.Rating))
            .ToListAsync(ct);
        Thread = new CommentThreadModel
        {
            Type = TargetType.Book,
            TargetId = book.Id,
            ViewerId = me,
            IsModerator = User.CanModerate(),
            Comments = await comments.GetAsync(TargetType.Book, book.Id, me, 200, ct),
        };
        return Page();
    }

    /// <summary>Saves the signed-in user's list entry. htmx swaps the editor in place; without JS it redirects back.</summary>
    public async Task<IActionResult> OnPostListAsync(string slug, ReadingStatus status, int? rating, int? pagesRead,
        DateOnly? startedOn, DateOnly? finishedOn, string? notes, bool isPrivate, CancellationToken ct)
    {
        if (CurrentUserId is not Guid me) return Challenge();
        var book = await db.Books.AsNoTracking().Where(b => b.Slug == slug && b.Status == BookStatus.Published)
            .Select(b => new { b.Id, b.Slug }).FirstOrDefaultAsync(ct);
        if (book is null) return NotFound();

        var entry = await lists.UpsertAsync(me, book.Id,
            new ListEntryInput(status, rating, pagesRead, startedOn, finishedOn, notes, isPrivate), ct);
        if (entry is null) return NotFound();

        return IsHtmx
            ? Partial("_ListEditor", EditorFrom(entry, book.Slug, saved: true))
            : RedirectToPage("/Books/Details", new { slug = book.Slug });
    }

    public async Task<IActionResult> OnPostRemoveAsync(string slug, CancellationToken ct)
    {
        if (CurrentUserId is not Guid me) return Challenge();
        var book = await db.Books.AsNoTracking().Where(b => b.Slug == slug).Select(b => new { b.Id, b.Slug }).FirstOrDefaultAsync(ct);
        if (book is null) return NotFound();

        await lists.RemoveAsync(me, book.Id, ct);
        return IsHtmx
            ? Partial("_ListEditor", new ListEditorModel { BookId = book.Id, Slug = book.Slug })
            : RedirectToPage("/Books/Details", new { slug = book.Slug });
    }

    private static ListEditorModel EditorFrom(ListEntry e, string slug, bool saved) => new()
    {
        BookId = e.BookId,
        Slug = slug,
        Exists = true,
        Status = e.Status,
        Rating = e.Rating,
        PagesRead = e.PagesRead,
        StartedOn = e.StartedOn,
        FinishedOn = e.FinishedOn,
        Notes = e.Notes,
        IsPrivate = e.IsPrivate,
        Saved = saved,
    };
}
