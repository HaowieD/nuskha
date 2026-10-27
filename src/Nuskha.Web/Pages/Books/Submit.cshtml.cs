namespace Nuskha.Web.Pages.Books;

/// <summary>
/// Any signed-in reader can propose a missing book. It stays out of the catalog until staff approve it. The submitter
/// can also reopen a pending or rejected submission (?edit={id}), fix it and send it back for review.
/// </summary>
[Authorize]
[EnableRateLimiting("write")]
[RequestSizeLimit(3 * 1024 * 1024)]
public class SubmitModel(NuskhaDbContext db, BookFormService forms, CatalogService catalog, UploadService uploads) : AppPageModel
{
    [BindProperty] public BookForm Form { get; set; } = new();
    [BindProperty] public Guid? EditId { get; set; }

    public bool Done { get; private set; }
    public string? DuplicateSlug { get; private set; }
    public string? CoverPath { get; private set; }
    public string? ReviewNote { get; private set; }
    public List<Genre> Genres { get; private set; } = [];

    public BookFormView View => new(Form, Errors, Genres, false, null, CoverPath);

    public async Task<IActionResult> OnGetAsync(Guid? edit, CancellationToken ct)
    {
        Genres = await forms.GenresAsync(ct);
        if (edit is not Guid id) return Page();

        var me = CurrentUserId!.Value;
        var book = await db.Books.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id && b.SubmittedById == me
            && (b.Status == BookStatus.Pending || b.Status == BookStatus.Rejected), ct);
        if (book is null) return NotFound();

        EditId = book.Id;
        CoverPath = book.CoverPath;
        ReviewNote = book.Status == BookStatus.Rejected ? book.ReviewNote : null;
        Form = await forms.LoadFormAsync(book, ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var me = CurrentUserId!.Value;
        Genres = await forms.GenresAsync(ct);

        Book? existing = null;
        if (EditId is Guid id)
        {
            existing = await db.Books.FirstOrDefaultAsync(b => b.Id == id && b.SubmittedById == me
                && (b.Status == BookStatus.Pending || b.Status == BookStatus.Rejected), ct);
            if (existing is null) return NotFound();
            CoverPath = existing.CoverPath;
        }
        else if (!User.IsStaff())
        {
            var pending = await db.Books.CountAsync(b => b.SubmittedById == me && b.Status == BookStatus.Pending, ct);
            if (pending >= BookReviewService.MaxPendingPerUser)
            {
                Errors["Form"] = "submit.limit";
                return Page();
            }
        }

        var valid = forms.Validate(Form, Errors, allowLanguage: false);
        if (valid is null) return Page();

        var duplicate = await catalog.FindDuplicateAsync(valid.Title, valid.Authors[0], valid.Isbn, existing?.Id, ct);
        if (duplicate is not null)
        {
            DuplicateSlug = duplicate.Status == BookStatus.Published ? duplicate.Slug : null;
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

        if (existing is null)
        {
            await forms.CreateAsync(valid, newCover, BookStatus.Pending, me, "user", tagsAsSubmitted: true, ct);
        }
        else
        {
            var oldCover = existing.CoverPath;
            if (newCover is not null) existing.CoverPath = newCover;
            else if (Form.RemoveCover) existing.CoverPath = null;
            // Resubmitting puts it back in the queue and clears the previous verdict.
            existing.Status = BookStatus.Pending;
            existing.ReviewNote = null;
            existing.ReviewedById = null;
            existing.ReviewedAt = null;
            await forms.UpdateAsync(existing, valid, tagsAsSubmitted: true, ct);
            if (oldCover is not null && oldCover != existing.CoverPath) uploads.Delete(oldCover);
        }

        Done = true;
        return Page();
    }
}
