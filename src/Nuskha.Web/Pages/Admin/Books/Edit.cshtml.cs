namespace Nuskha.Web.Pages.Admin.Books;

[Authorize(Policy = Policies.ManageCatalog)]
[RequestSizeLimit(3 * 1024 * 1024)]
public class EditModel(NuskhaDbContext db, CatalogService catalog, UploadService uploads) : AppPageModel
{
    [BindProperty] public string? Title { get; set; }
    [BindProperty] public string? TitleEn { get; set; }
    /// <summary>One author per line.</summary>
    [BindProperty] public string? Authors { get; set; }
    [BindProperty] public string? Isbn { get; set; }
    [BindProperty] public string? Publisher { get; set; }
    [BindProperty] public int? PublishYear { get; set; }
    [BindProperty] public int? PageCount { get; set; }
    [BindProperty] public string? Language { get; set; }
    [BindProperty] public string? Description { get; set; }
    [BindProperty] public BookStatus Status { get; set; }
    [BindProperty] public bool RemoveCover { get; set; }
    [BindProperty] public IFormFile? Cover { get; set; }

    public Guid BookId { get; private set; }
    public string Slug { get; private set; } = "";
    public string? CoverPath { get; private set; }
    public string? DuplicateSlug { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        var book = await db.Books.AsNoTracking().Include(b => b.Authors.OrderBy(a => a.SortOrder)).ThenInclude(a => a.Author)
            .FirstOrDefaultAsync(b => b.Id == id, ct);
        if (book is null) return NotFound();

        BookId = book.Id;
        Slug = book.Slug;
        CoverPath = book.CoverPath;
        Title = book.Title;
        TitleEn = book.TitleEn;
        Authors = string.Join("\n", book.Authors.Select(a => a.Author.Name));
        Isbn = book.Isbn;
        Publisher = book.Publisher;
        PublishYear = book.PublishYear;
        PageCount = book.PageCount;
        Language = book.Language;
        Description = book.Description;
        Status = book.Status;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken ct)
    {
        var book = await db.Books.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (book is null) return NotFound();
        BookId = book.Id;
        Slug = book.Slug;
        CoverPath = book.CoverPath;

        var title = TextUtil.NullIfBlank(Title);
        // Distinct by comparison key so "Name" and "name." cannot create the same BookAuthor key twice.
        var names = (Authors ?? "").Split(['\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(n => n.Length > 200 ? n[..200] : n)
            .DistinctBy(n => TextUtil.Key(n)).Where(n => TextUtil.Key(n).Length > 0).Take(8).ToList();
        var isbn = IsbnHelper.Normalize(Isbn);

        if (title is null) Errors[nameof(Title)] = "val.required";
        else if (title.Length > 300) Errors[nameof(Title)] = "val.toolong";
        if (names.Count == 0) Errors[nameof(Authors)] = "val.required";
        if (!string.IsNullOrWhiteSpace(Isbn) && isbn is null) Errors[nameof(Isbn)] = "val.isbn";
        if (PublishYear is not null && (PublishYear < 1000 || PublishYear > DateTime.UtcNow.Year + 1)) Errors[nameof(PublishYear)] = "val.year";
        if (PageCount is not null && (PageCount < 1 || PageCount > 20000)) Errors[nameof(PageCount)] = "val.number";
        if (Description is { Length: > 4000 }) Errors[nameof(Description)] = "val.toolong";
        if (!Enum.IsDefined(Status)) Errors[nameof(Status)] = "val.required";
        if (HasErrors) return Page();

        var duplicate = await catalog.FindDuplicateAsync(title!, names[0], isbn, id, ct);
        if (duplicate is not null)
        {
            DuplicateSlug = duplicate.Slug;
            Errors["Form"] = "submit.duplicate";
            return Page();
        }

        string? newCover = null;
        if (Cover is { Length: > 0 })
        {
            newCover = await uploads.SaveImageAsync(Cover, "covers", ct);
            if (newCover is null)
            {
                Errors[nameof(Cover)] = "val.image";
                return Page();
            }
        }

        var oldCover = book.CoverPath;
        book.Title = title!;
        book.TitleEn = TextUtil.NullIfBlank(TitleEn);
        book.Isbn = isbn;
        book.Publisher = TextUtil.NullIfBlank(Publisher);
        book.PublishYear = PublishYear;
        book.PageCount = PageCount;
        book.Language = string.IsNullOrWhiteSpace(Language) ? "ka" : Language.Trim().ToLowerInvariant()[..Math.Min(Language.Trim().Length, 8)];
        book.Description = TextUtil.NullIfBlank(Description);
        book.Status = Status;
        book.NormalizedKey = TextUtil.BookKey(title!, names[0]);
        book.UpdatedAt = DateTime.UtcNow;
        if (newCover is not null) book.CoverPath = newCover;
        else if (RemoveCover) book.CoverPath = null;

        // The BookAuthors are written with set-based SQL plus new rows, never through a tracked collection, so
        // replacing the same author with the same key cannot trip EF's identity map.
        var currentKeys = (await db.BookAuthors.Where(x => x.BookId == id).OrderBy(x => x.SortOrder)
            .Select(x => x.Author.NormalizedName).ToListAsync(ct));
        var authorsChanged = !currentKeys.SequenceEqual(names.Select(n => TextUtil.Key(n)));

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (authorsChanged)
        {
            await db.BookAuthors.Where(x => x.BookId == id).ExecuteDeleteAsync(ct);
            var order = 0;
            foreach (var name in names)
            {
                var author = await catalog.GetOrCreateAuthorAsync(name, ct);
                db.BookAuthors.Add(new BookAuthor { BookId = id, AuthorId = author.Id, SortOrder = order++ });
            }
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        if (oldCover is not null && oldCover != book.CoverPath) uploads.Delete(oldCover);
        TempData["Flash"] = "admin.saved";
        return RedirectToPage(new { id });
    }
}
