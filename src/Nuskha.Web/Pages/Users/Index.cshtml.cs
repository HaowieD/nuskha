namespace Nuskha.Web.Pages.Users;

public class IndexModel(NuskhaDbContext db) : AppPageModel
{
    public const int PageSize = 36;

    public record UserCard(string UserName, string DisplayName, string? AvatarPath, int BookCount, int Followers);

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true, Name = "p")] public int PageNo { get; set; } = 1;

    public List<UserCard> Items { get; private set; } = [];
    public int Total { get; private set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));

    public async Task OnGetAsync(CancellationToken ct)
    {
        PageNo = Math.Max(1, PageNo);
        var query = db.Users.AsNoTracking().Where(u => !u.LockoutEnd.HasValue || u.LockoutEnd < DateTimeOffset.UtcNow);
        if (!string.IsNullOrWhiteSpace(Q))
        {
            var like = "%" + WebExtensions.EscapeLike(Q.Trim()) + "%";
            query = query.Where(u => EF.Functions.ILike(u.UserName!, like) || EF.Functions.ILike(u.DisplayName, like));
        }

        Total = await query.CountAsync(ct);
        Items = await query
            .OrderByDescending(u => db.ListEntries.Count(e => e.UserId == u.Id && !e.IsPrivate)).ThenBy(u => u.UserName)
            .Skip((PageNo - 1) * PageSize).Take(PageSize)
            .Select(u => new UserCard(u.UserName!, u.DisplayName, u.AvatarPath,
                u.ListsArePublic ? db.ListEntries.Count(e => e.UserId == u.Id && !e.IsPrivate) : 0,
                db.Follows.Count(f => f.FolloweeId == u.Id)))
            .ToListAsync(ct);
    }
}
