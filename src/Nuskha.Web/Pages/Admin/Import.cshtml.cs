using Nuskha.Infrastructure.Import;

namespace Nuskha.Web.Pages.Admin;

/// <summary>Queues a background import from one of the configured book sources and shows recent runs.</summary>
[Authorize(Policy = Policies.ManageCatalog)]
public class ImportModel(NuskhaDbContext db, BookSourceRegistry registry, ImportQueue queue) : AppPageModel
{
    [BindProperty] public string? Source { get; set; }
    [BindProperty] public string? Query { get; set; }
    [BindProperty] public int Limit { get; set; } = 100;

    public IReadOnlyList<IBookSource> Sources => registry.All;
    public List<ImportRun> Runs { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct) => Runs = await LoadRunsAsync(ct);

    /// <summary>Polled by htmx so a running import's counters update without a reload.</summary>
    public async Task<IActionResult> OnGetRunsAsync(CancellationToken ct) => Partial("_ImportRuns", await LoadRunsAsync(ct));

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var source = Source is null ? null : registry.Find(Source);
        var query = TextUtil.NullIfBlank(Query);
        if (source is null) Errors[nameof(Source)] = "val.required";
        else if (!source.Enabled) Errors[nameof(Source)] = "admin.import.disabled";
        else if (source.RequiresQuery && query is null) Errors[nameof(Query)] = "val.required";
        if (Limit is < 1 or > 5000) Errors[nameof(Limit)] = "val.number";
        if (HasErrors)
        {
            Runs = await LoadRunsAsync(ct);
            return Page();
        }

        if (!queue.TryEnqueue(new ImportJob(source!.Id, query, Limit, CurrentUserId)))
        {
            Errors["Form"] = "admin.import.busy";
            Runs = await LoadRunsAsync(ct);
            return Page();
        }
        TempData["Flash"] = "admin.import.queued";
        return RedirectToPage();
    }

    private Task<List<ImportRun>> LoadRunsAsync(CancellationToken ct) =>
        db.ImportRuns.AsNoTracking().OrderByDescending(r => r.StartedAt).Take(15).ToListAsync(ct);
}
