namespace Nuskha.Web.Pages.Authors;

public class DetailsModel(NuskhaDbContext db) : AppPageModel
{
    public Author Author { get; private set; } = null!;
    public List<BookListItem> Books { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken ct)
    {
        var author = await db.Authors.AsNoTracking().FirstOrDefaultAsync(a => a.Slug == slug, ct);
        if (author is null) return NotFound();
        Author = author;
        Books = await db.Books.AsNoTracking()
            .Where(b => b.Status == BookStatus.Published && b.Authors.Any(a => a.AuthorId == author.Id))
            .OrderBy(b => b.PublishYear == null).ThenBy(b => b.PublishYear).ThenBy(b => b.Title)
            .ToListItems().ToListAsync(ct);
        return Page();
    }
}
