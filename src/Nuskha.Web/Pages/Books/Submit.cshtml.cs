namespace Nuskha.Web.Pages.Books;

/// <summary>Any signed-in reader can propose a missing book; it stays hidden until an editor or moderator publishes it.</summary>
[Authorize]
[EnableRateLimiting("write")]
[RequestSizeLimit(3 * 1024 * 1024)]
public class SubmitModel(NuskhaDbContext db, SlugService slugs, CatalogService catalog, UploadService uploads) : AppPageModel
{
    [BindProperty] public string? Title { get; set; }
    [BindProperty] public string? TitleEn { get; set; }
    /// <summary>One author per line.</summary>
    [BindProperty] public string? Authors { get; set; }
    [BindProperty] public string? Isbn { get; set; }
    [BindProperty] public string? Publisher { get; set; }
    [BindProperty] public int? PublishYear { get; set; }
    [BindProperty] public int? PageCount { get; set; }
    [BindProperty] public string? Description { get; set; }
    [BindProperty] public IFormFile? Cover { get; set; }

    public bool Done { get; private set; }
    public string? DuplicateSlug { get; private set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var title = TextUtil.NullIfBlank(Title);
        var authorNames = (Authors ?? "").Split(['\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var isbn = IsbnHelper.Normalize(Isbn);

        if (title is null) Errors[nameof(Title)] = "val.required";
        else if (title.Length > 300) Errors[nameof(Title)] = "val.toolong";
        if (authorNames.Count == 0) Errors[nameof(Authors)] = "val.required";
        if (!string.IsNullOrWhiteSpace(Isbn) && isbn is null) Errors[nameof(Isbn)] = "val.isbn";
        if (PublishYear is not null && (PublishYear < 1000 || PublishYear > DateTime.UtcNow.Year + 1)) Errors[nameof(PublishYear)] = "val.year";
        if (PageCount is not null && (PageCount < 1 || PageCount > 20000)) Errors[nameof(PageCount)] = "val.number";
        if (Description is { Length: > 4000 }) Errors[nameof(Description)] = "val.toolong";
        if (HasErrors) return Page();

        var duplicate = await catalog.FindDuplicateAsync(title!, authorNames.FirstOrDefault(), isbn, null, ct);
        if (duplicate is not null)
        {
            DuplicateSlug = duplicate.Status == BookStatus.Published ? duplicate.Slug : null;
            Errors["Form"] = "submit.duplicate";
            return Page();
        }

        string? coverPath = null;
        if (Cover is { Length: > 0 })
        {
            coverPath = await uploads.SaveImageAsync(Cover, "covers", ct);
            if (coverPath is null)
            {
                Errors[nameof(Cover)] = "val.image";
                return Page();
            }
        }

        var book = new Book
        {
            Title = title!,
            TitleEn = TextUtil.NullIfBlank(TitleEn),
            Slug = await slugs.BookSlugAsync(title!, null, ct),
            Isbn = isbn,
            Publisher = TextUtil.NullIfBlank(Publisher),
            PublishYear = PublishYear,
            PageCount = PageCount,
            Description = TextUtil.NullIfBlank(Description),
            CoverPath = coverPath,
            NormalizedKey = TextUtil.BookKey(title!, authorNames.FirstOrDefault()),
            Status = BookStatus.Pending,
            SubmittedById = CurrentUserId,
            Source = "user",
        };
        await catalog.SetAuthorsAsync(book, authorNames, ct);
        db.Books.Add(book);
        await db.SaveChangesAsync(ct);

        Done = true;
        return Page();
    }
}
