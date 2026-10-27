namespace Nuskha.Web.Pages.Admin;

/// <summary>The queue of reader-submitted books awaiting a decision. Any staff role (editor, moderator, admin) can work it.</summary>
public class ReviewModel(NuskhaDbContext db, BookReviewService review, CatalogService catalog) : AppPageModel
{
    public record Row(Guid Id, string Title, string? TitleEn, List<string> Authors, string? Isbn, string? Publisher, int? Year, int? Pages,
        string? Description, string? CoverPath, List<string> Genres, string? SubmittedTags, string? SubmittedBy, DateTime CreatedAt,
        string? DuplicateSlug, string? DuplicateTitle);

    public List<Row> Rows { get; private set; } = [];
    public int Total { get; private set; }
    public bool CanEdit => User.CanManageCatalog();

    public async Task OnGetAsync(CancellationToken ct) => await LoadAsync(ct);

    public async Task<IActionResult> OnPostApproveAsync(Guid id, string? note, CancellationToken ct)
    {
        if (!await review.ApproveAsync(id, CurrentUserId!.Value, note, ct)) return NotFound();
        TempData["Flash"] = "review.approved";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(Guid id, string? reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            Errors[id.ToString()] = "review.reason.required";
            await LoadAsync(ct);
            return Page();
        }
        if (!await review.RejectAsync(id, CurrentUserId!.Value, reason, ct)) return NotFound();
        TempData["Flash"] = "review.rejected";
        return RedirectToPage();
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        var pending = db.Books.AsNoTracking().Where(b => b.Status == BookStatus.Pending);
        Total = await pending.CountAsync(ct);
        var books = await pending.OrderBy(b => b.CreatedAt).Take(50)
            .Select(b => new
            {
                b.Id, b.Title, b.TitleEn, b.Isbn, b.Publisher, b.PublishYear, b.PageCount, b.Description, b.CoverPath, b.SubmittedTags, b.CreatedAt,
                Authors = b.Authors.OrderBy(a => a.SortOrder).Select(a => a.Author.Name).ToList(),
                Genres = b.Genres.OrderBy(g => g.Genre.SortOrder).Select(g => g.Genre.Name).ToList(),
                SubmittedBy = db.Users.Where(u => u.Id == b.SubmittedById).Select(u => u.UserName).FirstOrDefault(),
            }).ToListAsync(ct);

        foreach (var b in books)
        {
            var dup = await catalog.FindDuplicateAsync(b.Title, b.Authors.FirstOrDefault(), b.Isbn, b.Id, ct);
            Rows.Add(new Row(b.Id, b.Title, b.TitleEn, b.Authors, b.Isbn, b.Publisher, b.PublishYear, b.PageCount, b.Description, b.CoverPath,
                b.Genres, b.SubmittedTags, b.SubmittedBy, b.CreatedAt,
                dup is { Status: BookStatus.Published } ? dup.Slug : null, dup?.Title));
        }
    }
}
