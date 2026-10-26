namespace Nuskha.Web.Pages.Forum;

public class CategoryModel(NuskhaDbContext db) : AppPageModel
{
    public const int PageSize = 25;

    public record ThreadRow(string Slug, string Title, string AuthorUserName, string AuthorName, int PostCount,
        DateTime LastPostAt, bool IsPinned, bool IsLocked);

    [BindProperty(SupportsGet = true, Name = "p")] public int PageNo { get; set; } = 1;

    public ForumCategory Category { get; private set; } = null!;
    public List<ThreadRow> Threads { get; private set; } = [];
    public int Total { get; private set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken ct)
    {
        var category = await db.ForumCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Slug == slug, ct);
        if (category is null) return NotFound();
        Category = category;

        PageNo = Math.Max(1, PageNo);
        var query = db.ForumThreads.AsNoTracking().Where(t => t.CategoryId == category.Id);
        Total = await query.CountAsync(ct);
        Threads = await query.OrderByDescending(t => t.IsPinned).ThenByDescending(t => t.LastPostAt)
            .Skip((PageNo - 1) * PageSize).Take(PageSize)
            .Select(t => new ThreadRow(t.Slug, t.Title, t.Author.UserName!, t.Author.DisplayName, t.PostCount, t.LastPostAt, t.IsPinned, t.IsLocked))
            .ToListAsync(ct);
        return Page();
    }
}
