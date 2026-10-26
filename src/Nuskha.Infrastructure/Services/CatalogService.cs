using Microsoft.EntityFrameworkCore;
using Nuskha.Domain.Entities;
using Nuskha.Infrastructure.Data;

namespace Nuskha.Infrastructure.Services;

/// <summary>Shared catalog operations used by the importer, user submissions and the admin panel.</summary>
public class CatalogService(NuskhaDbContext db, SlugService slugs)
{
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
}
