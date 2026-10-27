namespace Nuskha.Web.Pages.Admin.Books;

public class IndexModel(NuskhaDbContext db, UploadService uploads, BookReviewService review) : AppPageModel
{
    public const int PageSize = 50;

    public record Row(Guid Id, string Slug, string Title, string? FirstAuthor, BookStatus Status, DateTime CreatedAt, string? Source, string? SubmittedBy);

    [BindProperty(SupportsGet = true)] public int? Status { get; set; }
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true, Name = "p")] public int PageNo { get; set; } = 1;

    public List<Row> Rows { get; private set; } = [];
    public int Total { get; private set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));

    public async Task OnGetAsync(CancellationToken ct)
    {
        PageNo = Math.Max(1, PageNo);
        var query = db.Books.AsNoTracking().AsQueryable();
        if (Status is int s && Enum.IsDefined((BookStatus)s)) query = query.Where(b => b.Status == (BookStatus)s);
        if (!string.IsNullOrWhiteSpace(Q))
        {
            var like = "%" + WebExtensions.EscapeLike(Q.Trim()) + "%";
            query = query.Where(b => EF.Functions.ILike(b.Title, like) || (b.TitleEn != null && EF.Functions.ILike(b.TitleEn, like))
                || b.Authors.Any(a => EF.Functions.ILike(a.Author.Name, like)));
        }

        Total = await query.CountAsync(ct);
        Rows = await query.OrderBy(b => b.Status).ThenByDescending(b => b.CreatedAt)
            .Skip((PageNo - 1) * PageSize).Take(PageSize)
            .Select(b => new Row(b.Id, b.Slug, b.Title,
                b.Authors.OrderBy(a => a.SortOrder).Select(a => a.Author.Name).FirstOrDefault(),
                b.Status, b.CreatedAt, b.Source,
                db.Users.Where(u => u.Id == b.SubmittedById).Select(u => u.UserName).FirstOrDefault()))
            .ToListAsync(ct);
    }

    public async Task<IActionResult> OnPostStatusAsync(Guid id, BookStatus status, int? filter, string? q)
    {
        if (!Enum.IsDefined(status) || status == BookStatus.Rejected) return BadRequest(); // rejecting needs a reason: use the review queue
        // Publishing something that was awaiting review is an approval, so the submitter gets told.
        if (status == BookStatus.Published && await review.ApproveAsync(id, CurrentUserId!.Value, null))
        {
            TempData["Flash"] = "admin.saved";
            return RedirectToPage(new { status = filter, q });
        }
        var updated = await db.Books.Where(b => b.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, status).SetProperty(b => b.UpdatedAt, DateTime.UtcNow));
        if (updated == 0) return NotFound();
        TempData["Flash"] = "admin.saved";
        return RedirectToPage(new { status = filter, q });
    }

    /// <summary>Hard delete is admin-only. List entries and activity cascade; comments and likes are polymorphic so are removed here.</summary>
    public async Task<IActionResult> OnPostDeleteAsync(Guid id, int? filter, string? q)
    {
        if (!User.IsInRole(Roles.Admin)) return Forbid();
        var book = await db.Books.FirstOrDefaultAsync(b => b.Id == id);
        if (book is null) return NotFound();

        var cover = book.CoverPath;
        await db.Comments.Where(c => c.TargetType == TargetType.Book && c.TargetId == id).ExecuteDeleteAsync();
        await db.Likes.Where(l => l.TargetType == TargetType.Book && l.TargetId == id).ExecuteDeleteAsync();
        db.Books.Remove(book);
        await db.SaveChangesAsync();
        uploads.Delete(cover);

        TempData["Flash"] = "admin.saved";
        return RedirectToPage(new { status = filter, q });
    }
}
