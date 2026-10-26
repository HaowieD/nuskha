using Microsoft.EntityFrameworkCore;
using Nuskha.Domain;
using Nuskha.Infrastructure.Data;

namespace Nuskha.Infrastructure.Services;

public record CommentVm(
    Guid Id, Guid? ParentId, Guid AuthorId, string UserName, string DisplayName, string? AvatarPath,
    string Body, DateTime CreatedAt, int LikeCount, bool Liked);

public class CommentQueryService(NuskhaDbContext db)
{
    public async Task<List<CommentVm>> GetAsync(TargetType type, Guid targetId, Guid? viewerId, int max = 200, CancellationToken ct = default)
    {
        var rows = await db.Comments.AsNoTracking()
            .Where(c => c.TargetType == type && c.TargetId == targetId && !c.IsDeleted)
            .OrderBy(c => c.CreatedAt).Take(max)
            .Select(c => new { c.Id, c.ParentId, c.AuthorId, c.Author.UserName, c.Author.DisplayName, c.Author.AvatarPath, c.Body, c.CreatedAt })
            .ToListAsync(ct);
        if (rows.Count == 0) return [];

        var ids = rows.Select(r => r.Id).ToList();
        var likes = await db.Likes.Where(l => l.TargetType == TargetType.Comment && ids.Contains(l.TargetId))
            .GroupBy(l => l.TargetId).Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, ct);
        var mine = viewerId is Guid v
            ? (await db.Likes.Where(l => l.UserId == v && l.TargetType == TargetType.Comment && ids.Contains(l.TargetId))
                .Select(l => l.TargetId).ToListAsync(ct)).ToHashSet()
            : [];

        return rows.Select(r => new CommentVm(r.Id, r.ParentId, r.AuthorId, r.UserName!, r.DisplayName, r.AvatarPath,
            r.Body, r.CreatedAt, likes.GetValueOrDefault(r.Id), mine.Contains(r.Id))).ToList();
    }
}
