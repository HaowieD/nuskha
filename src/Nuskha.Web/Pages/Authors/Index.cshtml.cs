namespace Nuskha.Web.Pages.Authors;

public class IndexModel(NuskhaDbContext db) : AppPageModel
{
    public const int PageSize = 48;

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true, Name = "p")] public int PageNo { get; set; } = 1;

    public List<AuthorListItem> Items { get; private set; } = [];
    public int Total { get; private set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));

    public async Task OnGetAsync(CancellationToken ct)
    {
        PageNo = Math.Max(1, PageNo);
        // Only authors that have at least one published book are listed.
        var query = db.Authors.AsNoTracking().Where(a => a.Books.Any(x => x.Book.Status == BookStatus.Published));
        if (!string.IsNullOrWhiteSpace(Q))
        {
            var like = "%" + WebExtensions.EscapeLike(Q.Trim()) + "%";
            query = query.Where(a => EF.Functions.ILike(a.Name, like) || (a.NameEn != null && EF.Functions.ILike(a.NameEn, like)));
        }

        Total = await query.CountAsync(ct);
        Items = await query
            .OrderBy(a => EF.Functions.Collate(a.Name, NuskhaDbContext.GeorgianCollation))
            .Skip((PageNo - 1) * PageSize).Take(PageSize)
            .Select(a => new AuthorListItem(a.Slug, a.Name, a.NameEn, a.Books.Count(x => x.Book.Status == BookStatus.Published)))
            .ToListAsync(ct);
    }
}
