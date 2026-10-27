namespace Nuskha.Web.Pages.Admin;

[Authorize(Policy = Policies.Moderate)]
public class ReportsModel(NuskhaDbContext db, SocialService social, FeedService feed) : AppPageModel
{
    public record Row(Guid Id, TargetType Type, Guid TargetId, string Reason, DateTime CreatedAt, string ReporterUserName, string? TargetUrl);

    [BindProperty(SupportsGet = true)] public bool ShowClosed { get; set; }

    public List<Row> Rows { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        var raw = await db.Reports.AsNoTracking()
            .Where(r => ShowClosed ? r.Status != ReportStatus.Open : r.Status == ReportStatus.Open)
            .OrderByDescending(r => r.CreatedAt).Take(100)
            .Select(r => new { r.Id, r.TargetType, r.TargetId, r.Reason, r.CreatedAt, ReporterUserName = r.Reporter.UserName! })
            .ToListAsync(ct);

        foreach (var r in raw)
        {
            var target = await social.ResolveTargetAsync(r.TargetType, r.TargetId, ct);
            Rows.Add(new Row(r.Id, r.TargetType, r.TargetId, r.Reason, r.CreatedAt, r.ReporterUserName, target?.Url));
        }
    }

    /// <summary>decision: "resolve" (acted on), "dismiss" (no action), or "delete" (remove the reported comment/post, then resolve).</summary>
    public async Task<IActionResult> OnPostAsync(Guid id, string decision, CancellationToken ct)
    {
        var me = CurrentUserId!.Value;
        var report = await db.Reports.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (report is null) return NotFound();

        switch (decision)
        {
            case "dismiss":
                report.Status = ReportStatus.Dismissed;
                break;
            case "resolve":
                report.Status = ReportStatus.Resolved;
                break;
            case "delete":
                if (report.TargetType == TargetType.Comment)
                    await social.DeleteCommentAsync(report.TargetId, me, isModerator: true, ct);
                else if (report.TargetType == TargetType.ForumPost)
                    await db.ForumPosts.Where(p => p.Id == report.TargetId).ExecuteUpdateAsync(s => s.SetProperty(p => p.IsDeleted, true), ct);
                else if (report.TargetType == TargetType.Activity)
                {
                    if (!await feed.DeleteStatusAsync(report.TargetId, me, isModerator: true, ct)) return BadRequest();
                }
                else
                    return BadRequest();
                report.Status = ReportStatus.Resolved;
                break;
            default:
                return BadRequest();
        }

        report.HandledById = me;
        report.HandledAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        TempData["Flash"] = "admin.saved";
        return RedirectToPage(new { showClosed = ShowClosed ? true : (bool?)null });
    }
}
