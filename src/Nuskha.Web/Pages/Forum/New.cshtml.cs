namespace Nuskha.Web.Pages.Forum;

[Authorize]
[EnableRateLimiting("write")]
public class NewModel(NuskhaDbContext db, SlugService slugs) : AppPageModel
{
    [BindProperty(SupportsGet = true)] public string? Category { get; set; }
    [BindProperty] public string? Title { get; set; }
    [BindProperty] public string? Body { get; set; }

    public List<ForumCategory> Categories { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct) => await LoadAsync(ct);

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        await LoadAsync(ct);
        var category = Categories.FirstOrDefault(c => c.Slug == Category);
        var title = Title?.Trim();
        var body = Body?.Trim();

        if (category is null) Errors[nameof(Category)] = "val.required";
        if (string.IsNullOrEmpty(title)) Errors[nameof(Title)] = "val.required";
        else if (title.Length > 200) Errors[nameof(Title)] = "val.toolong";
        if (string.IsNullOrEmpty(body)) Errors[nameof(Body)] = "val.required";
        else if (body.Length > 10000) Errors[nameof(Body)] = "val.toolong";
        if (HasErrors) return Page();

        var me = CurrentUserId!.Value;
        var thread = new ForumThread
        {
            CategoryId = category!.Id,
            Title = title!,
            Slug = await slugs.ThreadSlugAsync(title!, ct),
            AuthorId = me,
            PostCount = 1,
        };
        thread.Posts.Add(new ForumPost { Thread = thread, AuthorId = me, Body = body! });
        db.ForumThreads.Add(thread);
        db.Activities.Add(new ActivityEvent { UserId = me, Type = ActivityType.ThreadCreated, ThreadId = thread.Id });
        await db.SaveChangesAsync(ct);

        return Redirect("/forum/thread/" + thread.Slug);
    }

    private async Task LoadAsync(CancellationToken ct) =>
        Categories = await db.ForumCategories.AsNoTracking().OrderBy(c => c.SortOrder).ToListAsync(ct);
}
