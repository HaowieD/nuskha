namespace Nuskha.Web.Pages;

public class IndexModel(FeedService feed, NuskhaDbContext db) : AppPageModel
{
    public const int PageSize = 15;

    [BindProperty(SupportsGet = true)] public string? Scope { get; set; }
    public FeedPageModel Feed { get; private set; } = new();
    public List<BookListItem> Popular { get; private set; } = [];
    public List<BookListItem> Newest { get; private set; } = [];
    public bool Following => Scope == "following" && IsLoggedIn;

    public async Task OnGetAsync(CancellationToken ct)
    {
        Feed = await LoadFeedAsync(0, ct);
        var published = db.Books.AsNoTracking().Where(b => b.Status == BookStatus.Published);
        Popular = await published.OrderByDescending(b => b.ListCount).ThenByDescending(b => b.CreatedAt).Take(6).ToListItems().ToListAsync(ct);
        Newest = await published.OrderByDescending(b => b.CreatedAt).Take(6).ToListItems().ToListAsync(ct);
    }

    /// <summary>htmx "load more" endpoint for the feed.</summary>
    public async Task<IActionResult> OnGetFeedAsync(string? scope, int skip, CancellationToken ct)
    {
        Scope = scope;
        return Partial("_FeedList", await LoadFeedAsync(Math.Max(0, skip), ct));
    }

    private async Task<FeedPageModel> LoadFeedAsync(int skip, CancellationToken ct)
    {
        var items = await feed.GetAsync(Following ? FeedScope.Following : FeedScope.Global, CurrentUserId, null, skip, PageSize + 1, ct);
        var hasMore = items.Count > PageSize;
        if (hasMore) items.RemoveAt(items.Count - 1);
        return new FeedPageModel
        {
            Items = items,
            NextUrl = hasMore ? $"/?handler=Feed&scope={(Following ? "following" : "all")}&skip={skip + PageSize}" : null,
        };
    }
}
