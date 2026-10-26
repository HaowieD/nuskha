namespace Nuskha.Web.Pages.Admin.Authors;

[Authorize(Policy = Policies.ManageCatalog)]
public class IndexModel(NuskhaDbContext db) : AppPageModel
{
    public const int PageSize = 40;

    public record Row(Guid Id, string Slug, string Name, string? NameEn, string? Bio, int BookCount);

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true, Name = "p")] public int PageNo { get; set; } = 1;

    public List<Row> Rows { get; private set; } = [];
    public int Total { get; private set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));

    public async Task OnGetAsync(CancellationToken ct)
    {
        PageNo = Math.Max(1, PageNo);
        var query = db.Authors.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(Q))
        {
            var like = "%" + WebExtensions.EscapeLike(Q.Trim()) + "%";
            query = query.Where(a => EF.Functions.ILike(a.Name, like) || (a.NameEn != null && EF.Functions.ILike(a.NameEn, like)));
        }
        Total = await query.CountAsync(ct);
        Rows = await query.OrderBy(a => EF.Functions.Collate(a.Name, NuskhaDbContext.GeorgianCollation))
            .Skip((PageNo - 1) * PageSize).Take(PageSize)
            .Select(a => new Row(a.Id, a.Slug, a.Name, a.NameEn, a.Bio, a.Books.Count))
            .ToListAsync(ct);
    }

    public async Task<IActionResult> OnPostSaveAsync(Guid id, string? name, string? nameEn, string? bio, string? q, int p = 1)
    {
        var author = await db.Authors.FirstOrDefaultAsync(a => a.Id == id);
        if (author is null) return NotFound();

        name = TextUtil.NullIfBlank(name);
        nameEn = TextUtil.NullIfBlank(nameEn);
        bio = TextUtil.NullIfBlank(bio);
        if (name is null || name.Length > 200 || nameEn is { Length: > 200 } || bio is { Length: > 4000 })
            return Flash("admin.error.invalid", q, p);

        var key = TextUtil.Key(name);
        if (key.Length == 0 || await db.Authors.AnyAsync(a => a.NormalizedName == key && a.Id != id))
            return Flash("admin.error.duplicate", q, p);

        author.Name = name;
        author.NameEn = nameEn;
        author.Bio = bio;
        author.NormalizedName = key;
        await db.SaveChangesAsync();
        return Flash("admin.saved", q, p);
    }

    /// <summary>Only authors without books can be deleted, so a book never silently loses its author.</summary>
    public async Task<IActionResult> OnPostDeleteAsync(Guid id, string? q, int p = 1)
    {
        var deleted = await db.Authors.Where(a => a.Id == id && !a.Books.Any()).ExecuteDeleteAsync();
        return Flash(deleted == 1 ? "admin.saved" : "admin.error.hasbooks", q, p);
    }

    private IActionResult Flash(string key, string? q, int p)
    {
        TempData["Flash"] = key;
        return RedirectToPage(new { q, p = p > 1 ? p : (int?)null });
    }
}
