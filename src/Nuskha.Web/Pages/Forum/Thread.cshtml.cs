namespace Nuskha.Web.Pages.Forum;

public class ThreadModel(NuskhaDbContext db, SocialService social) : AppPageModel
{
    public const int PageSize = 20;

    public record PostVm(Guid Id, Guid AuthorId, string UserName, string DisplayName, string? AvatarPath, string Body,
        bool IsDeleted, DateTime CreatedAt, DateTime? EditedAt, int LikeCount, bool Liked);

    [BindProperty(SupportsGet = true, Name = "p")] public int PageNo { get; set; } = 1;

    public ForumThread Thread { get; private set; } = null!;
    public ForumCategory Category { get; private set; } = null!;
    public List<PostVm> Posts { get; private set; } = [];
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Thread.PostCount / (double)PageSize));
    public string? ReplyError { get; private set; }

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken ct) =>
        await LoadAsync(slug, ct) ? Page() : NotFound();

    public async Task<IActionResult> OnPostReplyAsync(string slug, string? body, CancellationToken ct)
    {
        if (CurrentUserId is not Guid me) return Challenge();
        var thread = await db.ForumThreads.FirstOrDefaultAsync(t => t.Slug == slug, ct);
        if (thread is null) return NotFound();

        body = body?.Trim();
        if (thread.IsLocked && !User.CanModerate()) return await ReplyFailedAsync(slug, "forum.locked.msg", ct);
        if (string.IsNullOrEmpty(body)) return await ReplyFailedAsync(slug, "val.required", ct);
        if (body.Length > 10000) return await ReplyFailedAsync(slug, "val.toolong", ct);

        var post = new ForumPost { ThreadId = thread.Id, AuthorId = me, Body = body };
        db.ForumPosts.Add(post);
        thread.PostCount++;
        thread.LastPostAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var lastPage = Math.Max(1, (int)Math.Ceiling(thread.PostCount / (double)PageSize));
        var url = $"/forum/thread/{thread.Slug}?p={lastPage}#post-{post.Id}";
        await social.NotifyAsync(thread.AuthorId, me, NotificationType.Reply, url, ct);
        return Redirect(url);
    }

    public async Task<IActionResult> OnPostModerateAsync(string slug, string action, CancellationToken ct)
    {
        if (!User.CanModerate()) return Forbid();
        var thread = await db.ForumThreads.FirstOrDefaultAsync(t => t.Slug == slug, ct);
        if (thread is null) return NotFound();

        switch (action)
        {
            case "pin": thread.IsPinned = true; break;
            case "unpin": thread.IsPinned = false; break;
            case "lock": thread.IsLocked = true; break;
            case "unlock": thread.IsLocked = false; break;
            default: return BadRequest();
        }
        await db.SaveChangesAsync(ct);
        return RedirectToPage(new { slug });
    }

    /// <summary>Authors may remove their own posts; moderators may remove any. Soft delete keeps the thread readable.</summary>
    public async Task<IActionResult> OnPostDeletePostAsync(string slug, Guid postId, CancellationToken ct)
    {
        if (CurrentUserId is not Guid me) return Challenge();
        var post = await db.ForumPosts.FirstOrDefaultAsync(p => p.Id == postId && p.Thread.Slug == slug, ct);
        if (post is null) return NotFound();
        if (post.AuthorId != me && !User.CanModerate()) return Forbid();

        post.IsDeleted = true;
        await db.SaveChangesAsync(ct);
        return RedirectToPage(new { slug, p = PageNo > 1 ? PageNo : (int?)null });
    }

    private async Task<IActionResult> ReplyFailedAsync(string slug, string errorKey, CancellationToken ct)
    {
        await LoadAsync(slug, ct);
        ReplyError = errorKey;
        return Page();
    }

    private async Task<bool> LoadAsync(string slug, CancellationToken ct)
    {
        var thread = await db.ForumThreads.AsNoTracking().Include(t => t.Category).Include(t => t.Author)
            .FirstOrDefaultAsync(t => t.Slug == slug, ct);
        if (thread is null) return false;
        Thread = thread;
        Category = thread.Category;

        PageNo = Math.Clamp(PageNo, 1, TotalPages);
        var rows = await db.ForumPosts.AsNoTracking().Where(p => p.ThreadId == thread.Id)
            .OrderBy(p => p.CreatedAt).Skip((PageNo - 1) * PageSize).Take(PageSize)
            .Select(p => new { p.Id, p.AuthorId, p.Author.UserName, p.Author.DisplayName, p.Author.AvatarPath, p.Body, p.IsDeleted, p.CreatedAt, p.EditedAt })
            .ToListAsync(ct);

        var ids = rows.Select(r => r.Id).ToList();
        var likes = await db.Likes.Where(l => l.TargetType == TargetType.ForumPost && ids.Contains(l.TargetId))
            .GroupBy(l => l.TargetId).Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, ct);
        var mine = CurrentUserId is Guid viewer
            ? (await db.Likes.Where(l => l.UserId == viewer && l.TargetType == TargetType.ForumPost && ids.Contains(l.TargetId))
                .Select(l => l.TargetId).ToListAsync(ct)).ToHashSet()
            : [];

        Posts = rows.Select(r => new PostVm(r.Id, r.AuthorId, r.UserName!, r.DisplayName, r.AvatarPath, r.Body, r.IsDeleted,
            r.CreatedAt, r.EditedAt, likes.GetValueOrDefault(r.Id), mine.Contains(r.Id))).ToList();
        return true;
    }
}
