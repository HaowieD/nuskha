namespace Nuskha.Web.Pages.Admin;

/// <summary>Staff manage the fixed genre list and clean up free-form tags (rename, merge duplicates, delete).</summary>
[Authorize(Policy = Policies.ManageCatalog)]
public class TaxonomyModel(NuskhaDbContext db, SlugService slugs) : AppPageModel
{
    public record GenreRow(Guid Id, string Slug, string Name, string? NameEn, int SortOrder, int BookCount);
    public record TagRow(Guid Id, string Slug, string Name, int BookCount);

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }

    public List<GenreRow> Genres { get; private set; } = [];
    public List<TagRow> Tags { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct) => await LoadAsync(ct);

    public async Task<IActionResult> OnPostAddGenreAsync(string? name, string? nameEn, int sortOrder, CancellationToken ct)
    {
        name = TextUtil.NullIfBlank(name);
        nameEn = TextUtil.NullIfBlank(nameEn);
        if (name is null || name.Length > 100 || nameEn is { Length: > 100 }) return await FailAsync("val.required", ct);

        db.Genres.Add(new Genre { Name = name, NameEn = nameEn, SortOrder = sortOrder, Slug = await slugs.GenreSlugAsync(nameEn ?? name, null, ct) });
        await db.SaveChangesAsync(ct);
        return Saved();
    }

    public async Task<IActionResult> OnPostSaveGenreAsync(Guid id, string? name, string? nameEn, int sortOrder, CancellationToken ct)
    {
        name = TextUtil.NullIfBlank(name);
        nameEn = TextUtil.NullIfBlank(nameEn);
        if (name is null || name.Length > 100 || nameEn is { Length: > 100 }) return await FailAsync("val.required", ct);

        // The slug stays as it is, so existing links to /genres/{slug} keep working after a rename.
        var updated = await db.Genres.Where(g => g.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.Name, name).SetProperty(g => g.NameEn, nameEn).SetProperty(g => g.SortOrder, sortOrder), ct);
        return updated == 0 ? NotFound() : Saved();
    }

    public async Task<IActionResult> OnPostDeleteGenreAsync(Guid id, CancellationToken ct)
    {
        var deleted = await db.Genres.Where(g => g.Id == id).ExecuteDeleteAsync(ct); // BookGenres cascade
        return deleted == 0 ? NotFound() : Saved();
    }

    /// <summary>Renames a tag. If another tag already has that name the two are merged.</summary>
    public async Task<IActionResult> OnPostRenameTagAsync(Guid id, string? name, CancellationToken ct)
    {
        name = TextUtil.NullIfBlank(name);
        if (name is null || name.Length > 60 || TextUtil.Key(name).Length == 0) return await FailAsync("val.required", ct);
        var tag = await db.Tags.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tag is null) return NotFound();

        var key = TextUtil.Key(name);
        var other = await db.Tags.FirstOrDefaultAsync(t => t.NormalizedName == key && t.Id != id, ct);
        if (other is null)
        {
            tag.Name = name;
            tag.NormalizedName = key;
            await db.SaveChangesAsync(ct);
            return Saved();
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var alreadyOnTarget = db.BookTags.Where(x => x.TagId == other.Id).Select(x => x.BookId);
        await db.BookTags.Where(x => x.TagId == id && !alreadyOnTarget.Contains(x.BookId))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.TagId, other.Id), ct);
        await db.Tags.Where(t => t.Id == id).ExecuteDeleteAsync(ct); // leftover duplicate BookTags cascade
        await tx.CommitAsync(ct);
        return Saved();
    }

    public async Task<IActionResult> OnPostDeleteTagAsync(Guid id, CancellationToken ct)
    {
        var deleted = await db.Tags.Where(t => t.Id == id).ExecuteDeleteAsync(ct);
        return deleted == 0 ? NotFound() : Saved();
    }

    private IActionResult Saved()
    {
        TempData["Flash"] = "admin.saved";
        return RedirectToPage(new { q = Q });
    }

    private async Task<IActionResult> FailAsync(string key, CancellationToken ct)
    {
        Errors["Form"] = key;
        await LoadAsync(ct);
        return Page();
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        Genres = await db.Genres.AsNoTracking().OrderBy(g => g.SortOrder).ThenBy(g => g.Name)
            .Select(g => new GenreRow(g.Id, g.Slug, g.Name, g.NameEn, g.SortOrder, g.Books.Count)).ToListAsync(ct);

        var tags = db.Tags.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(Q))
        {
            var like = "%" + WebExtensions.EscapeLike(Q.Trim()) + "%";
            tags = tags.Where(t => EF.Functions.ILike(t.Name, like));
        }
        Tags = await tags.OrderByDescending(t => t.Books.Count).ThenBy(t => t.Name).Take(200)
            .Select(t => new TagRow(t.Id, t.Slug, t.Name, t.Books.Count)).ToListAsync(ct);
    }
}
