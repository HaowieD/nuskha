namespace Nuskha.Web.Pages.Books;

/// <summary>Entry point for browsing the catalog by genre and by popular tags.</summary>
public class BrowseModel(NuskhaDbContext db) : AppPageModel
{
    public record GenreRow(string Slug, string Name, string? NameEn, int BookCount);
    public record TagRow(string Slug, string Name, int BookCount);

    public List<GenreRow> Genres { get; private set; } = [];
    public List<TagRow> Tags { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        Genres = await db.Genres.AsNoTracking().OrderBy(g => g.SortOrder).ThenBy(g => g.Name)
            .Select(g => new GenreRow(g.Slug, g.Name, g.NameEn, g.Books.Count(x => x.Book.Status == BookStatus.Published)))
            .ToListAsync(ct);
        // Filter and order on entity properties first; EF cannot translate a Where/OrderBy over a projected record.
        Tags = await db.Tags.AsNoTracking()
            .Where(t => t.Books.Any(x => x.Book.Status == BookStatus.Published))
            .OrderByDescending(t => t.Books.Count(x => x.Book.Status == BookStatus.Published)).ThenBy(t => t.Name)
            .Take(60)
            .Select(t => new TagRow(t.Slug, t.Name, t.Books.Count(x => x.Book.Status == BookStatus.Published)))
            .ToListAsync(ct);
    }
}
