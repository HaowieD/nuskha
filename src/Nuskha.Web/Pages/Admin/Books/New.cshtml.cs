namespace Nuskha.Web.Pages.Admin.Books;

/// <summary>Staff add a single book by hand. Unlike reader submissions it can be published straight away.</summary>
[Authorize(Policy = Policies.ManageCatalog)]
[RequestSizeLimit(3 * 1024 * 1024)]
public class NewModel(BookFormService forms, CatalogService catalog, UploadService uploads) : AppPageModel
{
    private static readonly BookStatus[] Options = [BookStatus.Published, BookStatus.Pending];

    [BindProperty] public BookForm Form { get; set; } = new() { Language = "ka" };

    public string? DuplicateSlug { get; private set; }
    public List<Genre> Genres { get; private set; } = [];

    public BookFormView View => new(Form, Errors, Genres, true, Options, null);

    public async Task OnGetAsync(CancellationToken ct) => Genres = await forms.GenresAsync(ct);

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        Genres = await forms.GenresAsync(ct);
        if (!Options.Contains(Form.Status)) Form.Status = BookStatus.Published;

        var valid = forms.Validate(Form, Errors, allowLanguage: true);
        if (valid is null) return Page();

        var duplicate = await catalog.FindDuplicateAsync(valid.Title, valid.Authors[0], valid.Isbn, null, ct);
        if (duplicate is not null)
        {
            DuplicateSlug = duplicate.Slug;
            Errors["Form"] = "submit.duplicate";
            return Page();
        }

        string? cover = null;
        if (Form.Cover is { Length: > 0 })
        {
            cover = await uploads.SaveImageAsync(Form.Cover, "covers", ct);
            if (cover is null)
            {
                Errors["Cover"] = "val.image";
                return Page();
            }
        }

        var book = await forms.CreateAsync(valid, cover, Form.Status, CurrentUserId, "admin", tagsAsSubmitted: false, ct);
        TempData["Flash"] = "admin.saved";
        return RedirectToPage("/Admin/Books/Edit", new { id = book.Id });
    }
}
