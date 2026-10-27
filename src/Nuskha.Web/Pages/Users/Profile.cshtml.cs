namespace Nuskha.Web.Pages.Users;

public class ProfileModel(NuskhaDbContext db, FeedService feed) : AppPageModel
{
    public const int PageSize = 15;

    public AppUser Profile { get; private set; } = null!;
    public bool IsSelf { get; private set; }
    public bool CanSeeList { get; private set; }
    public bool Following { get; private set; }
    public int Followers { get; private set; }
    public int FollowingCount { get; private set; }
    public List<string> StaffRoles { get; private set; } = [];
    public Dictionary<ReadingStatus, int> StatusCounts { get; private set; } = [];
    public double? AverageRating { get; private set; }
    public List<BookListItem> Top { get; private set; } = [];
    public FeedPageModel Feed { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(string username, CancellationToken ct)
    {
        var user = await FindAsync(username, ct);
        if (user is null) return NotFound();
        // One canonical URL per user, whatever the casing typed.
        if (!string.Equals(user.UserName, username, StringComparison.Ordinal)) return RedirectPermanent("/user/" + user.UserName);

        Profile = user;
        var me = CurrentUserId;
        IsSelf = me == user.Id;
        CanSeeList = user.ListsArePublic || IsSelf || User.CanModerate();

        Followers = await db.Follows.CountAsync(f => f.FolloweeId == user.Id, ct);
        FollowingCount = await db.Follows.CountAsync(f => f.FollowerId == user.Id, ct);
        Following = me is Guid viewer && await db.Follows.AnyAsync(f => f.FollowerId == viewer && f.FolloweeId == user.Id, ct);

        var roleNames = await (from ur in db.UserRoles
                               join r in db.Roles on ur.RoleId equals r.Id
                               where ur.UserId == user.Id
                               select r.Name).ToListAsync(ct);
        StaffRoles = roleNames.Where(r => r is Roles.Admin or Roles.Moderator or Roles.Editor).Select(r => r!).ToList();

        if (CanSeeList)
        {
            var visible = db.ListEntries.AsNoTracking().Where(e => e.UserId == user.Id && (IsSelf || !e.IsPrivate) && e.Book.Status == BookStatus.Published);
            StatusCounts = (await visible.GroupBy(e => e.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(ct))
                .ToDictionary(x => x.Status, x => x.Count);
            AverageRating = await visible.Where(e => e.Rating != null).AverageAsync(e => (double?)e.Rating, ct);
            Top = await visible.Where(e => e.Rating != null)
                .OrderByDescending(e => e.Rating).ThenByDescending(e => e.UpdatedAt).Take(6)
                .Select(e => e.Book).ToListItems().ToListAsync(ct);
        }

        // Status posts are public even when the reading list is private; the feed query hides list activity itself.
        Feed = await LoadFeedAsync(user, 0, ct);
        return Page();
    }

    /// <summary>htmx "load more" for the activity list.</summary>
    public async Task<IActionResult> OnGetFeedAsync(string username, int skip, CancellationToken ct)
    {
        var user = await FindAsync(username, ct);
        if (user is null) return NotFound();
        return Partial("_FeedList", await LoadFeedAsync(user, Math.Max(0, skip), ct));
    }

    private Task<AppUser?> FindAsync(string username, CancellationToken ct)
    {
        var normalized = username.ToUpperInvariant();
        return db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.NormalizedUserName == normalized, ct);
    }

    private async Task<FeedPageModel> LoadFeedAsync(AppUser user, int skip, CancellationToken ct)
    {
        var items = await feed.GetAsync(FeedScope.User, CurrentUserId, user.Id, skip, PageSize + 1, ct);
        var hasMore = items.Count > PageSize;
        if (hasMore) items.RemoveAt(items.Count - 1);
        return new FeedPageModel
        {
            Items = items,
            NextUrl = hasMore ? $"/user/{user.UserName}?handler=Feed&skip={skip + PageSize}" : null,
        };
    }
}
