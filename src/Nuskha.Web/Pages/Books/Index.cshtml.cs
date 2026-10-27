namespace Nuskha.Web.Pages.Books;

public class IndexModel(NuskhaDbContext db) : AppPageModel
{
    public const int PageSize = 24;

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? Sort { get; set; }
    [BindProperty(SupportsGet = true, Name = "p")] public int PageNo { get; set; } = 1;
    /// <summary>Genre slug; reachable as /books?genre=x or /genres/x.</summary>
    [BindProperty(SupportsGet = true)] public string? Genre { get; set; }
    /// <summary>Tag slug; reachable as /books?tag=x or /tags/x.</summary>
    [BindProperty(SupportsGet = true)] public string? Tag { get; set; }

    public record GenreChip(string Slug, string Name, string? NameEn);
    public List<GenreChip> Genres { get; private set; } = [];
    public GenreChip? ActiveGenre { get; private set; }
    public string? ActiveTagName { get; private set; }

    public List<BookListItem> Books { get; private set; } = [];
    public int Total { get; private set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        PageNo = Math.Max(1, PageNo);
        var query = db.Books.AsNoTracking().Where(b => b.Status == BookStatus.Published);

        Genres = await db.Genres.AsNoTracking().OrderBy(g => g.SortOrder).ThenBy(g => g.Name)
            .Select(g => new GenreChip(g.Slug, g.Name, g.NameEn)).ToListAsync(ct);
        if (!string.IsNullOrWhiteSpace(Genre))
        {
            ActiveGenre = Genres.FirstOrDefault(g => g.Slug == Genre);
            if (ActiveGenre is null) return NotFound();
            query = query.Where(b => b.Genres.Any(x => x.Genre.Slug == Genre));
        }
        if (!string.IsNullOrWhiteSpace(Tag))
        {
            ActiveTagName = await db.Tags.AsNoTracking().Where(t => t.Slug == Tag).Select(t => t.Name).FirstOrDefaultAsync(ct);
            if (ActiveTagName is null) return NotFound();
            query = query.Where(b => b.Tags.Any(x => x.Tag.Slug == Tag));
        }

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
        return Page();
    }
}
