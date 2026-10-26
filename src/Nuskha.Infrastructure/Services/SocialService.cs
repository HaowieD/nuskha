using Microsoft.EntityFrameworkCore;
using Nuskha.Domain;
using Nuskha.Domain.Entities;
using Nuskha.Infrastructure.Data;

namespace Nuskha.Infrastructure.Services;

public record TargetInfo(Guid? OwnerId, string Url);
public record LikeState(bool Liked, int Count);

/// <summary>Likes, comments, follows and notifications — one implementation for every target type.</summary>
public class SocialService(NuskhaDbContext db)
{
    public const int MaxCommentLength = 2000;

    /// <summary>Resolves who owns a target and where to link to it. Null when the target does not exist / is not visible.</summary>
    public async Task<TargetInfo?> ResolveTargetAsync(TargetType type, Guid id, CancellationToken ct = default)
    {
        switch (type)
        {
            case TargetType.Profile:
            {
                var name = await db.Users.Where(u => u.Id == id).Select(u => u.UserName).FirstOrDefaultAsync(ct);
                return name is null ? null : new TargetInfo(id, $"/user/{name}");
            }
            case TargetType.Book:
            {
                var slug = await db.Books.Where(b => b.Id == id && b.Status == BookStatus.Published).Select(b => b.Slug).FirstOrDefaultAsync(ct);
                return slug is null ? null : new TargetInfo(null, $"/books/{slug}");
            }
            case TargetType.ListEntry:
            {
                var e = await db.ListEntries.Where(x => x.Id == id && !x.IsPrivate)
                    .Select(x => new { x.UserId, x.User.UserName }).FirstOrDefaultAsync(ct);
                return e is null ? null : new TargetInfo(e.UserId, $"/user/{e.UserName}/list");
            }
            case TargetType.Activity:
            {
                var e = await db.Activities.Where(x => x.Id == id)
                    .Select(x => new { x.UserId, x.User.UserName }).FirstOrDefaultAsync(ct);
                return e is null ? null : new TargetInfo(e.UserId, $"/user/{e.UserName}");
            }
            case TargetType.ForumThread:
            {
                var t = await db.ForumThreads.Where(x => x.Id == id).Select(x => new { x.AuthorId, x.Slug }).FirstOrDefaultAsync(ct);
                return t is null ? null : new TargetInfo(t.AuthorId, $"/forum/thread/{t.Slug}");
            }
            case TargetType.ForumPost:
            {
                var p = await db.ForumPosts.Where(x => x.Id == id && !x.IsDeleted)
                    .Select(x => new { x.AuthorId, x.Thread.Slug }).FirstOrDefaultAsync(ct);
                return p is null ? null : new TargetInfo(p.AuthorId, $"/forum/thread/{p.Slug}#post-{id}");
            }
            case TargetType.Comment:
            {
                var c = await db.Comments.Where(x => x.Id == id && !x.IsDeleted)
                    .Select(x => new { x.AuthorId, x.TargetType, x.TargetId }).FirstOrDefaultAsync(ct);
                if (c is null) return null;
                var parent = await ResolveTargetAsync(c.TargetType, c.TargetId, ct);
                return parent is null ? null : new TargetInfo(c.AuthorId, parent.Url);
            }
            default:
                return null;
        }
    }

    // ---------- likes ----------

    public async Task<LikeState?> ToggleLikeAsync(Guid userId, TargetType type, Guid targetId, CancellationToken ct = default)
    {
        var target = await ResolveTargetAsync(type, targetId, ct);
        if (target is null) return null;

        var existing = await db.Likes.FirstOrDefaultAsync(l => l.UserId == userId && l.TargetType == type && l.TargetId == targetId, ct);
        var added = false;
        if (existing is not null) db.Likes.Remove(existing);
        else { db.Likes.Add(new Like { UserId = userId, TargetType = type, TargetId = targetId }); added = true; }

        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); added = false; } // double click race

        if (added && target.OwnerId is Guid owner && owner != userId)
            await NotifyAsync(owner, userId, NotificationType.Like, target.Url, ct);

        return await GetLikeStateAsync(userId, type, targetId, ct);
    }

    public async Task<LikeState> GetLikeStateAsync(Guid? userId, TargetType type, Guid targetId, CancellationToken ct = default)
    {
        var count = await db.Likes.CountAsync(l => l.TargetType == type && l.TargetId == targetId, ct);
        var liked = userId is Guid u && await db.Likes.AnyAsync(l => l.UserId == u && l.TargetType == type && l.TargetId == targetId, ct);
        return new LikeState(liked, count);
    }

    // ---------- comments ----------

    public async Task<Comment?> AddCommentAsync(Guid authorId, TargetType type, Guid targetId, string? body, Guid? parentId, CancellationToken ct = default)
    {
        body = body?.Trim();
        if (string.IsNullOrEmpty(body) || body.Length > MaxCommentLength) return null;
        var target = await ResolveTargetAsync(type, targetId, ct);
        if (target is null) return null;

        Guid? replyToAuthor = null;
        Guid? topParent = null;
        if (parentId is Guid pid)
        {
            var parent = await db.Comments.Where(c => c.Id == pid && c.TargetType == type && c.TargetId == targetId && !c.IsDeleted)
                .Select(c => new { c.Id, c.ParentId, c.AuthorId }).FirstOrDefaultAsync(ct);
            if (parent is null) return null;
            topParent = parent.ParentId ?? parent.Id; // keep threads one level deep
            replyToAuthor = parent.AuthorId;
        }

        var comment = new Comment { AuthorId = authorId, TargetType = type, TargetId = targetId, Body = body, ParentId = topParent };
        db.Comments.Add(comment);
        await db.SaveChangesAsync(ct);

        if (replyToAuthor is Guid ra && ra != authorId)
            await NotifyAsync(ra, authorId, NotificationType.Reply, target.Url, ct);
        if (target.OwnerId is Guid owner && owner != authorId && owner != replyToAuthor)
            await NotifyAsync(owner, authorId, NotificationType.Comment, target.Url, ct);

        return comment;
    }

    public async Task<bool> DeleteCommentAsync(Guid commentId, Guid actorId, bool isModerator, CancellationToken ct = default)
    {
        var c = await db.Comments.FirstOrDefaultAsync(x => x.Id == commentId, ct);
        if (c is null || c.IsDeleted) return false;
        if (c.AuthorId != actorId && !isModerator) return false;
        c.IsDeleted = true;
        await db.SaveChangesAsync(ct);
        return true;
    }

    // ---------- follows ----------

    public async Task<bool?> ToggleFollowAsync(Guid followerId, Guid followeeId, CancellationToken ct = default)
    {
        if (followerId == followeeId) return null;
        var target = await ResolveTargetAsync(TargetType.Profile, followeeId, ct);
        if (target is null) return null;

        var existing = await db.Follows.FirstOrDefaultAsync(f => f.FollowerId == followerId && f.FolloweeId == followeeId, ct);
        if (existing is not null)
        {
            db.Follows.Remove(existing);
            await db.SaveChangesAsync(ct);
            return false;
        }
        db.Follows.Add(new Follow { FollowerId = followerId, FolloweeId = followeeId });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); return true; }
        var followerName = await db.Users.Where(u => u.Id == followerId).Select(u => u.UserName).FirstAsync(ct);
        await NotifyAsync(followeeId, followerId, NotificationType.Follow, $"/user/{followerName}", ct);
        return true;
    }

    // ---------- notifications ----------

    public async Task NotifyAsync(Guid recipientId, Guid actorId, NotificationType type, string url, CancellationToken ct = default)
    {
        if (recipientId == actorId) return;
        var duplicate = await db.Notifications.AnyAsync(n =>
            n.UserId == recipientId && n.ActorId == actorId && n.Type == type && n.Url == url && !n.IsRead, ct);
        if (duplicate) return;
        db.Notifications.Add(new Notification { UserId = recipientId, ActorId = actorId, Type = type, Url = url });
        await db.SaveChangesAsync(ct);
    }
}
