using Microsoft.EntityFrameworkCore;
using Nuskha.Domain;
using Nuskha.Domain.Entities;
using Nuskha.Infrastructure.Data;

namespace Nuskha.Infrastructure.Services;

public enum FeedScope { Global, Following, User }

public class ActivityItem
{
    public Guid Id { get; set; }
    public ActivityType Type { get; set; }
    public string? Data { get; set; }
    /// <summary>Markup of a status post; render it with the Markdown renderer.</summary>
    public string? Body { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? AvatarPath { get; set; }
    public Guid? RecipientId { get; set; }
    public string? RecipientUserName { get; set; }
    public string? RecipientDisplayName { get; set; }
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
    public const int MaxStatusLength = 3000;

    public async Task<List<ActivityItem>> GetAsync(FeedScope scope, Guid? viewerId, Guid? profileUserId,
        int skip, int take, CancellationToken ct = default)
    {
        // People who keep their list private must not leak what they read through the feeds; their own posts and threads are public.
        var q = db.Activities.AsNoTracking()
            .Where(a => a.Type == ActivityType.StatusPost || a.Type == ActivityType.ThreadCreated
                        || a.User.ListsArePublic || a.UserId == viewerId);
        switch (scope)
        {
            case FeedScope.Following:
                if (viewerId is not Guid me) return [];
                q = q.Where(a => a.UserId == me || db.Follows.Any(f => f.FollowerId == me && f.FolloweeId == a.UserId));
                break;
            case FeedScope.User:
                // A profile shows what its owner did plus messages other people wrote on it.
                q = q.Where(a => a.UserId == profileUserId || a.RecipientId == profileUserId);
                break;
        }

        var items = await Project(q.OrderByDescending(a => a.CreatedAt).Skip(skip).Take(take)).ToListAsync(ct);
        await DecorateAsync(items, viewerId, ct);
        return items;
    }

    public async Task<ActivityItem?> GetOneAsync(Guid id, Guid? viewerId, CancellationToken ct = default)
    {
        var items = await Project(db.Activities.AsNoTracking().Where(a => a.Id == id)).ToListAsync(ct);
        await DecorateAsync(items, viewerId, ct);
        return items.FirstOrDefault();
    }

    /// <summary>Creates a status post on the author's own profile, or on another user's profile when a recipient is given.</summary>
    public async Task<Guid?> PostStatusAsync(Guid authorId, string? body, string? recipientUserName, CancellationToken ct = default)
    {
        body = body?.Trim();
        if (string.IsNullOrEmpty(body) || body.Length > MaxStatusLength) return null;

        Guid? recipientId = null;
        string? recipientName = null;
        if (!string.IsNullOrWhiteSpace(recipientUserName))
        {
            var normalized = recipientUserName.Trim().ToUpperInvariant();
            var recipient = await db.Users.Where(u => u.NormalizedUserName == normalized)
                .Select(u => new { u.Id, u.UserName }).FirstOrDefaultAsync(ct);
            if (recipient is null) return null;
            if (recipient.Id != authorId) { recipientId = recipient.Id; recipientName = recipient.UserName; }
        }

        var activity = new ActivityEvent { UserId = authorId, Type = ActivityType.StatusPost, Body = body, RecipientId = recipientId };
        db.Activities.Add(activity);
        if (recipientId is Guid rid)
            db.Notifications.Add(new Notification
            {
                UserId = rid, ActorId = authorId, Type = NotificationType.Message,
                Url = $"/user/{recipientName}#activity-{activity.Id}",
            });
        await db.SaveChangesAsync(ct);
        return activity.Id;
    }

    /// <summary>Deletes a status post. Allowed for its author, the profile owner it was written on, and moderators.</summary>
    public async Task<bool> DeleteStatusAsync(Guid activityId, Guid actorId, bool isModerator, CancellationToken ct = default)
    {
        var a = await db.Activities.Where(x => x.Id == activityId && x.Type == ActivityType.StatusPost)
            .Select(x => new { x.UserId, x.RecipientId }).FirstOrDefaultAsync(ct);
        if (a is null) return false;
        if (!isModerator && a.UserId != actorId && a.RecipientId != actorId) return false;

        // Comments and likes point at the activity polymorphically, so they are not covered by a foreign key cascade.
        await db.Comments.Where(c => c.TargetType == TargetType.Activity && c.TargetId == activityId).ExecuteDeleteAsync(ct);
        await db.Likes.Where(l => l.TargetType == TargetType.Activity && l.TargetId == activityId).ExecuteDeleteAsync(ct);
        await db.Activities.Where(x => x.Id == activityId).ExecuteDeleteAsync(ct);
        return true;
    }

    private static IQueryable<ActivityItem> Project(IQueryable<ActivityEvent> q) =>
        q.Select(a => new ActivityItem
        {
            Id = a.Id,
            Type = a.Type,
            Data = a.Data,
            Body = a.Body,
            CreatedAt = a.CreatedAt,
            UserId = a.UserId,
            UserName = a.User.UserName!,
            DisplayName = a.User.DisplayName,
            AvatarPath = a.User.AvatarPath,
            RecipientId = a.RecipientId,
            RecipientUserName = a.Recipient != null ? a.Recipient.UserName : null,
            RecipientDisplayName = a.Recipient != null ? a.Recipient.DisplayName : null,
            BookTitle = a.Book != null ? a.Book.Title : null,
            BookTitleEn = a.Book != null ? a.Book.TitleEn : null,
            BookSlug = a.Book != null ? a.Book.Slug : null,
            ThreadTitle = a.Thread != null ? a.Thread.Title : null,
            ThreadSlug = a.Thread != null ? a.Thread.Slug : null,
        });

    private async Task DecorateAsync(List<ActivityItem> items, Guid? viewerId, CancellationToken ct)
    {
        if (items.Count == 0) return;
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
    }
}
