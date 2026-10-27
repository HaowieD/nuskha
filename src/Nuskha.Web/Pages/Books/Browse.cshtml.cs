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
        Tags = await db.Tags.AsNoTracking()
            .Select(t => new TagRow(t.Slug, t.Name, t.Books.Count(x => x.Book.Status == BookStatus.Published)))
            .Where(t => t.BookCount > 0)
            .OrderByDescending(t => t.BookCount).ThenBy(t => t.Name).Take(60).ToListAsync(ct);
    }
}
