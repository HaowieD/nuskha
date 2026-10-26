namespace Nuskha.Web.Pages.Forum;

public class IndexModel(NuskhaDbContext db) : AppPageModel
{
    public record CategoryRow(string Slug, string Name, string? NameEn, string? Description, string? DescriptionEn,
        int ThreadCount, int PostCount, string? LastTitle, string? LastSlug, DateTime? LastPostAt);

    public List<CategoryRow> Categories { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct) =>
        Categories = await db.ForumCategories.AsNoTracking().OrderBy(c => c.SortOrder)
            .Select(c => new CategoryRow(c.Slug, c.Name, c.NameEn, c.Description, c.DescriptionEn,
                c.Threads.Count,
                c.Threads.Sum(t => t.PostCount),
                c.Threads.OrderByDescending(t => t.LastPostAt).Select(t => t.Title).FirstOrDefault(),
                c.Threads.OrderByDescending(t => t.LastPostAt).Select(t => t.Slug).FirstOrDefault(),
                c.Threads.Max(t => (DateTime?)t.LastPostAt)))
            .ToListAsync(ct);
}
