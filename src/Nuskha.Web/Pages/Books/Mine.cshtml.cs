namespace Nuskha.Web.Pages.Books;

/// <summary>The signed-in reader's own submissions and what staff decided about them.</summary>
[Authorize]
public class MineModel(NuskhaDbContext db) : AppPageModel
{
    public record Item(Guid Id, string Slug, string Title, BookStatus Status, string? ReviewNote, DateTime CreatedAt);

    public List<Item> Items { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        var me = CurrentUserId!.Value;
        Items = await db.Books.AsNoTracking().Where(b => b.SubmittedById == me && b.Source == "user")
            .OrderByDescending(b => b.CreatedAt).Take(100)
            .Select(b => new Item(b.Id, b.Slug, b.Title, b.Status, b.ReviewNote, b.CreatedAt))
            .ToListAsync(ct);
    }
}
