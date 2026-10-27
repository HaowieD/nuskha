namespace Nuskha.Web.Pages;

[Authorize]
[EnableRateLimiting("write")]
public class ReportModel(NuskhaDbContext db, SocialService social) : AppPageModel
{
    private static readonly TargetType[] Reportable =
        [TargetType.Comment, TargetType.ForumPost, TargetType.ForumThread, TargetType.Profile, TargetType.Book, TargetType.Activity];

    [BindProperty(SupportsGet = true)] public int Type { get; set; }
    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }
    [BindProperty] public string? Reason { get; set; }
    public string? TargetUrl { get; private set; }
    public string? Error { get; private set; }
    public bool Done { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct) => await LoadTargetAsync(ct) ?? Page();

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (await LoadTargetAsync(ct) is { } failure) return failure;
        var reason = Reason?.Trim();
        if (string.IsNullOrEmpty(reason) || reason.Length < 5)
        {
            Error = "report.tooshort";
            return Page();
        }
        var me = CurrentUserId!.Value;
        var t = (TargetType)Type;
        var duplicate = await db.Reports.AnyAsync(r => r.ReporterId == me && r.TargetType == t && r.TargetId == Id && r.Status == ReportStatus.Open, ct);
        if (!duplicate)
        {
            db.Reports.Add(new Report { ReporterId = me, TargetType = t, TargetId = Id, Reason = reason.Length > 1000 ? reason[..1000] : reason });
            await db.SaveChangesAsync(ct);
        }
        Done = true;
        return Page();
    }

    private async Task<IActionResult?> LoadTargetAsync(CancellationToken ct)
    {
        if (!Enum.IsDefined((TargetType)Type) || !Reportable.Contains((TargetType)Type)) return BadRequest();
        var target = await social.ResolveTargetAsync((TargetType)Type, Id, ct);
        if (target is null) return NotFound();
        TargetUrl = target.Url;
        return null;
    }
}
