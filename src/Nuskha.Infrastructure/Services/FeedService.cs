using Microsoft.EntityFrameworkCore;
using Nuskha.Domain;
using Nuskha.Infrastructure.Data;

namespace Nuskha.Infrastructure.Services;

public enum FeedScope { Global, Following, User }

public class ActivityItem
{
    public Guid Id { get; set; }
    public ActivityType Type { get; set; }
    public string? Data { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? AvatarPath { get; set; }
    public string? BookTitle { get; set; }
    public string? BookTitleEn { get; set; }
    public string? BookSlug { get; set; }
    public string? ThreadTitle { get; set; }
    public string? ThreadSlug { get; set; }
    public int LikeCount { get; set; }
    public bool Liked { get; set; }
    public int CommentCount { get; set; }
}

public class FeedService(NuskhaDbContext db)
{
    public async Task<List<ActivityItem>> GetAsync(FeedScope scope, Guid? viewerId, Guid? profileUserId,
        int skip, int take, CancellationToken ct = default)
    {
        var q = db.Activities.AsNoTracking().AsQueryable();
        switch (scope)
        {
            case FeedScope.Following:
                if (viewerId is not Guid me) return [];
                q = q.Where(a => a.UserId == me || db.Follows.Any(f => f.FollowerId == me && f.FolloweeId == a.UserId));
                break;
            case FeedScope.User:
                q = q.Where(a => a.UserId == profileUserId);
                break;
        }

        var items = await q.OrderByDescending(a => a.CreatedAt).Skip(skip).Take(take)
            .Select(a => new ActivityItem
            {
                Id = a.Id,
                Type = a.Type,
                Data = a.Data,
                CreatedAt = a.CreatedAt,
                UserId = a.UserId,
                UserName = a.User.UserName!,
                DisplayName = a.User.DisplayName,
                AvatarPath = a.User.AvatarPath,
                BookTitle = a.Book != null ? a.Book.Title : null,
                BookTitleEn = a.Book != null ? a.Book.TitleEn : null,
                BookSlug = a.Book != null ? a.Book.Slug : null,
                ThreadTitle = a.Thread != null ? a.Thread.Title : null,
                ThreadSlug = a.Thread != null ? a.Thread.Slug : null,
            }).ToListAsync(ct);

        if (items.Count == 0) return items;
        var ids = items.Select(i => i.Id).ToList();

        var likes = await db.Likes.Where(l => l.TargetType == TargetType.Activity && ids.Contains(l.TargetId))
            .GroupBy(l => l.TargetId).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, ct);
        var comments = await db.Comments.Where(c => c.TargetType == TargetType.Activity && !c.IsDeleted && ids.Contains(c.TargetId))
            .GroupBy(c => c.TargetId).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, ct);
        var mine = viewerId is Guid v
            ? (await db.Likes.Where(l => l.UserId == v && l.TargetType == TargetType.Activity && ids.Contains(l.TargetId))
                .Select(l => l.TargetId).ToListAsync(ct)).ToHashSet()
            : [];

        foreach (var i in items)
        {
            i.LikeCount = likes.GetValueOrDefault(i.Id);
            i.CommentCount = comments.GetValueOrDefault(i.Id);
            i.Liked = mine.Contains(i.Id);
        }
        return items;
    }
}
