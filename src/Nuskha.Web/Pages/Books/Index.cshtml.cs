namespace Nuskha.Web.Pages.Books;

public class IndexModel(NuskhaDbContext db) : AppPageModel
{
    public const int PageSize = 24;

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? Sort { get; set; }
    [BindProperty(SupportsGet = true, Name = "p")] public int PageNo { get; set; } = 1;

    public List<BookListItem> Books { get; private set; } = [];
    public int Total { get; private set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));

    public async Task OnGetAsync(CancellationToken ct)
    {
        PageNo = Math.Max(1, PageNo);
        var query = db.Books.AsNoTracking().Where(b => b.Status == BookStatus.Published);

        if (!string.IsNullOrWhiteSpace(Q))
        {
            var term = Q.Trim();
            var like = "%" + WebExtensions.EscapeLike(term) + "%";
            var isbn = IsbnHelper.Normalize(term);
            query = query.Where(b =>
                EF.Functions.ILike(b.Title, like)
                || (b.TitleEn != null && EF.Functions.ILike(b.TitleEn, like))
                || b.Authors.Any(a => EF.Functions.ILike(a.Author.Name, like) || (a.Author.NameEn != null && EF.Functions.ILike(a.Author.NameEn, like)))
                || (isbn != null && b.Isbn == isbn));
        }

        IQueryable<Book> ordered = Sort switch
        {
            "new" => query.OrderByDescending(b => b.CreatedAt),
            "rating" => query.OrderByDescending(b => b.RatingCount == 0 ? 0d : (double)b.RatingSum / b.RatingCount).ThenByDescending(b => b.RatingCount),
            "title" => query.OrderBy(b => EF.Functions.Collate(b.Title, NuskhaDbContext.GeorgianCollation)),
            _ => query.OrderByDescending(b => b.ListCount).ThenByDescending(b => b.CreatedAt),
        };

        Total = await ordered.CountAsync(ct);
        Books = await ordered.Skip((PageNo - 1) * PageSize).Take(PageSize).ToListItems().ToListAsync(ct);
    }
}
