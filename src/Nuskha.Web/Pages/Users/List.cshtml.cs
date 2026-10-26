namespace Nuskha.Web.Pages.Users;

public class ListModel(NuskhaDbContext db) : AppPageModel
{
    public const int PageSize = 40;

    public record Row(Guid BookId, string Slug, string Title, string? TitleEn, string? CoverPath, string? FirstAuthor,
        ReadingStatus Status, int? Rating, int? PagesRead, int? PageCount, DateOnly? FinishedOn, string? Notes, bool IsPrivate);

    [BindProperty(SupportsGet = true)] public int? Status { get; set; }
    [BindProperty(SupportsGet = true)] public string? Sort { get; set; }
    [BindProperty(SupportsGet = true, Name = "p")] public int PageNo { get; set; } = 1;

    public AppUser Profile { get; private set; } = null!;
    public List<Row> Rows { get; private set; } = [];
    public Dictionary<ReadingStatus, int> Counts { get; private set; } = [];
    public int Total { get; private set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
    public bool IsSelf { get; private set; }

    public async Task<IActionResult> OnGetAsync(string username, CancellationToken ct)
    {
        var normalized = username.ToUpperInvariant();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.NormalizedUserName == normalized, ct);
        if (user is null) return NotFound();
        if (!string.Equals(user.UserName, username, StringComparison.Ordinal)) return RedirectPermanent($"/user/{user.UserName}/list");

        Profile = user;
        IsSelf = CurrentUserId == user.Id;
        if (!(user.ListsArePublic || IsSelf || User.CanModerate())) return RedirectToPage("/Users/Profile", new { username = user.UserName });

        var visible = db.ListEntries.AsNoTracking()
            .Where(e => e.UserId == user.Id && (IsSelf || !e.IsPrivate) && e.Book.Status == BookStatus.Published);
        Counts = (await visible.GroupBy(e => e.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(ct))
            .ToDictionary(x => x.Status, x => x.Count);

        var filtered = visible;
        if (Status is int s && Enum.IsDefined((ReadingStatus)s)) filtered = filtered.Where(e => e.Status == (ReadingStatus)s);

        IOrderedQueryable<ListEntry> ordered = Sort switch
        {
            "rating" => filtered.OrderByDescending(e => e.Rating ?? 0).ThenByDescending(e => e.UpdatedAt),
            "title" => filtered.OrderBy(e => EF.Functions.Collate(e.Book.Title, NuskhaDbContext.GeorgianCollation)),
            _ => filtered.OrderByDescending(e => e.UpdatedAt),
        };

        PageNo = Math.Max(1, PageNo);
        Total = await filtered.CountAsync(ct);
        Rows = await ordered.Skip((PageNo - 1) * PageSize).Take(PageSize)
            .Select(e => new Row(e.BookId, e.Book.Slug, e.Book.Title, e.Book.TitleEn, e.Book.CoverPath,
                e.Book.Authors.OrderBy(a => a.SortOrder).Select(a => a.Author.Name).FirstOrDefault(),
                e.Status, e.Rating, e.PagesRead, e.Book.PageCount, e.FinishedOn, e.Notes, e.IsPrivate))
            .ToListAsync(ct);
        return Page();
    }
}
