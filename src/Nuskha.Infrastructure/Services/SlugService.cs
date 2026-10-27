using Microsoft.EntityFrameworkCore;
using Nuskha.Infrastructure.Data;

namespace Nuskha.Infrastructure.Services;

/// <summary>Generates unique, readable URL identifiers (slugs) for books, authors, threads.</summary>
public class SlugService(NuskhaDbContext db)
{
    private static readonly HashSet<string> ReservedBook = ["submit", "new", "edit", "mine"];
    private static readonly HashSet<string> ReservedForum = ["thread", "new"];

    public Task<string> BookSlugAsync(string title, Guid? exceptId = null, CancellationToken ct = default) =>
        UniqueAsync(TextUtil.Slugify(title, 80), ReservedBook,
            (s, token) => db.Books.AnyAsync(b => b.Slug == s && b.Id != exceptId, token), ct);

    public Task<string> AuthorSlugAsync(string name, Guid? exceptId = null, CancellationToken ct = default) =>
        UniqueAsync(TextUtil.Slugify(name, 80), [],
            (s, token) => db.Authors.AnyAsync(a => a.Slug == s && a.Id != exceptId, token), ct);

    public Task<string> TagSlugAsync(string name, CancellationToken ct = default) =>
        UniqueAsync(TextUtil.Slugify(name, 60), [],
            (s, token) => db.Tags.AnyAsync(t => t.Slug == s, token), ct);

    public Task<string> GenreSlugAsync(string name, Guid? exceptId = null, CancellationToken ct = default) =>
        UniqueAsync(TextUtil.Slugify(name, 60), [],
            (s, token) => db.Genres.AnyAsync(g => g.Slug == s && g.Id != exceptId, token), ct);

    public Task<string> ThreadSlugAsync(string title, CancellationToken ct = default) =>
        UniqueAsync(TextUtil.Slugify(title, 80), ReservedForum,
            (s, token) => db.ForumThreads.AnyAsync(t => t.Slug == s, token), ct);

    private static async Task<string> UniqueAsync(string baseSlug, HashSet<string> reserved,
        Func<string, CancellationToken, Task<bool>> exists, CancellationToken ct)
    {
        var candidate = baseSlug;
        for (var i = 2; i < 100; i++)
        {
            if (!reserved.Contains(candidate) && !await exists(candidate, ct)) return candidate;
            candidate = $"{baseSlug}-{i}";
        }
        return $"{baseSlug}-{Guid.NewGuid().ToString("N")[..8]}";
    }
}
