namespace Nuskha.Web.Pages.Admin;

public class IndexModel(NuskhaDbContext db) : AppPageModel
{
    public int UserCount { get; private set; }
    public int BookCount { get; private set; }
    public int PendingBooks { get; private set; }
    public int AuthorCount { get; private set; }
    public int OpenReports { get; private set; }
    public int ThreadCount { get; private set; }
    public int EntryCount { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        UserCount = await db.Users.CountAsync(ct);
        BookCount = await db.Books.CountAsync(b => b.Status == BookStatus.Published, ct);
        PendingBooks = await db.Books.CountAsync(b => b.Status == BookStatus.Pending, ct);
        AuthorCount = await db.Authors.CountAsync(ct);
        OpenReports = await db.Reports.CountAsync(r => r.Status == ReportStatus.Open, ct);
        ThreadCount = await db.ForumThreads.CountAsync(ct);
        EntryCount = await db.ListEntries.CountAsync(ct);
    }
}
