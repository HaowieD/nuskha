namespace Nuskha.Web.Pages.Me;

public class NotificationsModel(NuskhaDbContext db) : AppPageModel
{
    public record Item(NotificationType Type, string Url, bool IsRead, DateTime CreatedAt, string ActorName, string ActorUserName, string? ActorAvatar);

    public List<Item> Items { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        var me = CurrentUserId!.Value;
        Items = await db.Notifications.AsNoTracking()
            .Where(n => n.UserId == me)
            .OrderByDescending(n => n.CreatedAt).Take(50)
            .Select(n => new Item(n.Type, n.Url, n.IsRead, n.CreatedAt, n.Actor.DisplayName, n.Actor.UserName!, n.Actor.AvatarPath))
            .ToListAsync(ct);

        // The list above still shows what was unread; opening the page is what marks it as read.
        await db.Notifications.Where(n => n.UserId == me && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);
    }
}
