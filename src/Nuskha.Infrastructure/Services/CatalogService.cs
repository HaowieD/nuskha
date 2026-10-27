using Microsoft.EntityFrameworkCore;
using Nuskha.Domain.Entities;
using Nuskha.Infrastructure.Data;

namespace Nuskha.Infrastructure.Services;

/// <summary>Shared catalog operations used by the importer, user submissions and the admin panel.</summary>
public class CatalogService(NuskhaDbContext db, SlugService slugs)
{
    public const int MaxTagsPerBook = 12;
    public const int MaxGenresPerBook = 4;

    public async Task<Author> GetOrCreateAuthorAsync(string name, CancellationToken ct = default)
    {
        name = name.Trim();
        var norm = TextUtil.Key(name);
        var author = await db.Authors.FirstOrDefaultAsync(a => a.NormalizedName == norm, ct);
        if (author is not null) return author;
        author = new Author { Name = name, NormalizedName = norm, Slug = await slugs.AuthorSlugAsync(name, null, ct) };
        db.Authors.Add(author);
        await db.SaveChangesAsync(ct);
        return author;
    }

    /// <summary>Replaces the authors of a tracked book (its Authors collection must be loaded).</summary>
    public async Task SetAuthorsAsync(Book book, IEnumerable<string> names, CancellationToken ct = default)
    {
        var wanted = names.Select(n => n.Trim()).Where(n => n.Length > 0).Select(n => n.Length > 200 ? n[..200] : n)
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToList();
        book.Authors.Clear();
        var order = 0;
        foreach (var name in wanted)
        {
            var author = await GetOrCreateAuthorAsync(name, ct);
            if (book.Authors.All(a => a.AuthorId != author.Id))
                book.Authors.Add(new BookAuthor { BookId = book.Id, AuthorId = author.Id, SortOrder = order++ });
        }
    }

    public async Task<Book?> FindDuplicateAsync(string title, string? firstAuthor, string? isbn, Guid? exceptId = null, CancellationToken ct = default)
    {
        if (isbn is not null)
        {
            var byIsbn = await db.Books.FirstOrDefaultAsync(b => b.Isbn == isbn && b.Id != exceptId, ct);
            if (byIsbn is not null) return byIsbn;
        }
        var key = TextUtil.BookKey(title, firstAuthor);
        return await db.Books.FirstOrDefaultAsync(b => b.NormalizedKey == key && b.Id != exceptId, ct);
    }

    // ---------- genres and tags ----------

    /// <summary>Splits "war, road trip; history" into clean, distinct tag names (max 60 chars, at most <see cref="MaxTagsPerBook"/>).</summary>
    public static List<string> ParseTagNames(string? raw) =>
        (raw ?? "").Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => string.Join(' ', t.Split(' ', StringSplitOptions.RemoveEmptyEntries)))
            .Where(t => t.Length > 0)
            .Select(t => t.Length > 60 ? t[..60] : t)
            .DistinctBy(TextUtil.Key).Where(t => TextUtil.Key(t).Length > 0)
            .Take(MaxTagsPerBook).ToList();

    public async Task<Tag> GetOrCreateTagAsync(string name, CancellationToken ct = default)
    {
        name = name.Trim();
        var norm = TextUtil.Key(name);
        var tag = await db.Tags.FirstOrDefaultAsync(t => t.NormalizedName == norm, ct);
        if (tag is not null) return tag;
        tag = new Tag { Name = name, NormalizedName = norm, Slug = await slugs.TagSlugAsync(name, ct) };
        db.Tags.Add(tag);
        await db.SaveChangesAsync(ct);
        return tag;
    }

    /// <summary>Replaces a book's tags. Uses set-based deletes and new rows so it is safe on books loaded without their collections.</summary>
    public async Task SetTagsAsync(Guid bookId, IEnumerable<string> names, CancellationToken ct = default)
    {
        var wanted = new List<Guid>();
        foreach (var name in names.Take(MaxTagsPerBook))
            wanted.Add((await GetOrCreateTagAsync(name, ct)).Id);

        await db.BookTags.Where(x => x.BookId == bookId).ExecuteDeleteAsync(ct);
        foreach (var id in wanted.Distinct())
            db.BookTags.Add(new BookTag { BookId = bookId, TagId = id });
        await db.SaveChangesAsync(ct);
    }

    public async Task SetGenresAsync(Guid bookId, IEnumerable<Guid> genreIds, CancellationToken ct = default)
    {
        var requested = genreIds.Distinct().Take(MaxGenresPerBook).ToList();
        var valid = await db.Genres.Where(g => requested.Contains(g.Id)).Select(g => g.Id).ToListAsync(ct);
        await db.BookGenres.Where(x => x.BookId == bookId).ExecuteDeleteAsync(ct);
        foreach (var id in valid)
            db.BookGenres.Add(new BookGenre { BookId = bookId, GenreId = id });
        await db.SaveChangesAsync(ct);
    }
}
