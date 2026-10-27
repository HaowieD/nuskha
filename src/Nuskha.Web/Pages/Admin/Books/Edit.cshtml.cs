namespace Nuskha.Web.Pages.Admin.Books;

[Authorize(Policy = Policies.ManageCatalog)]
[RequestSizeLimit(3 * 1024 * 1024)]
public class EditModel(NuskhaDbContext db, BookFormService forms, CatalogService catalog, BookReviewService review, UploadService uploads) : AppPageModel
{
    [BindProperty] public BookForm Form { get; set; } = new();

    public Guid BookId { get; private set; }
    public string Slug { get; private set; } = "";
    public string? CoverPath { get; private set; }
    public string? DuplicateSlug { get; private set; }
    public string? SubmittedBy { get; private set; }
    public List<Genre> Genres { get; private set; } = [];

    public BookFormView View => new(Form, Errors, Genres, true, Enum.GetValues<BookStatus>(), CoverPath);

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        var book = await db.Books.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, ct);
        if (book is null) return NotFound();
        await FillAsync(book, ct);
        Form = await forms.LoadFormAsync(book, ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken ct)
    {
        var book = await db.Books.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (book is null) return NotFound();
        await FillAsync(book, ct);

        if (!Enum.IsDefined(Form.Status)) Errors["Status"] = "val.required";
        var valid = forms.Validate(Form, Errors, allowLanguage: true);
        if (valid is null) return Page();

        var duplicate = await catalog.FindDuplicateAsync(valid.Title, valid.Authors[0], valid.Isbn, id, ct);
        if (duplicate is not null)
        {
            DuplicateSlug = duplicate.Slug;
            Errors["Form"] = "submit.duplicate";
            return Page();
        }

        string? newCover = null;
        if (Form.Cover is { Length: > 0 })
        {
            newCover = await uploads.SaveImageAsync(Form.Cover, "covers", ct);
            if (newCover is null)
            {
                Errors["Cover"] = "val.image";
                return Page();
            }
        }

        var oldCover = book.CoverPath;
        if (newCover is not null) book.CoverPath = newCover;
        else if (Form.RemoveCover) book.CoverPath = null;

        // Publishing a book that was waiting for review goes through the review service so the submitter is told.
        var approving = book.Status != BookStatus.Published && Form.Status == BookStatus.Published;
        if (!approving) book.Status = Form.Status;

        await forms.UpdateAsync(book, valid, tagsAsSubmitted: false, ct);
        if (approving) await review.ApproveAsync(book.Id, CurrentUserId!.Value, null, ct);

        if (oldCover is not null && oldCover != book.CoverPath) uploads.Delete(oldCover);
        TempData["Flash"] = "admin.saved";
        return RedirectToPage(new { id });
    }

    private async Task FillAsync(Book book, CancellationToken ct)
    {
        BookId = book.Id;
        Slug = book.Slug;
        CoverPath = book.CoverPath;
        Genres = await forms.GenresAsync(ct);
        SubmittedBy = book.SubmittedById is Guid sid
            ? await db.Users.Where(u => u.Id == sid).Select(u => u.UserName).FirstOrDefaultAsync(ct)
            : null;
    }
}
