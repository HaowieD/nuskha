namespace Nuskha.Web.Services;

/// <summary>A book form after validation and normalisation.</summary>
public sealed record ValidBook(string Title, string? TitleEn, List<string> Authors, string? Isbn, string? Publisher,
    int? Year, int? Pages, string Language, string? Description, List<string> Tags, List<Guid> GenreIds);

/// <summary>
/// One place that validates the book form and writes a book with its authors, genres and tags, so reader submissions,
/// resubmissions and the admin create/edit pages cannot drift apart.
/// </summary>
public class BookFormService(NuskhaDbContext db, CatalogService catalog, SlugService slugs)
{
    public Task<List<Genre>> GenresAsync(CancellationToken ct = default) =>
        db.Genres.AsNoTracking().OrderBy(g => g.SortOrder).ThenBy(g => g.Name).ToListAsync(ct);

    public ValidBook? Validate(BookForm f, IDictionary<string, string> errors, bool allowLanguage)
    {
        var title = TextUtil.NullIfBlank(f.Title);
        var authors = (f.Authors ?? "").Split(['\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(n => n.Length > 200 ? n[..200] : n)
            // Distinct by comparison key so "Name" and "name." cannot produce the same BookAuthor key twice.
            .DistinctBy(TextUtil.Key).Where(n => TextUtil.Key(n).Length > 0).Take(8).ToList();
        var isbn = IsbnHelper.Normalize(f.Isbn);
        var tags = CatalogService.ParseTagNames(f.Tags);
        var genreIds = f.GenreIds.Distinct().ToList();

        if (title is null) errors["Title"] = "val.required";
        else if (title.Length > 300) errors["Title"] = "val.toolong";
        if (f.TitleEn is { Length: > 300 }) errors["TitleEn"] = "val.toolong";
        if (authors.Count == 0) errors["Authors"] = "val.required";
        if (!string.IsNullOrWhiteSpace(f.Isbn) && isbn is null) errors["Isbn"] = "val.isbn";
        if (f.Publisher is { Length: > 200 }) errors["Publisher"] = "val.toolong";
        if (f.PublishYear is not null && (f.PublishYear < 1000 || f.PublishYear > DateTime.UtcNow.Year + 1)) errors["PublishYear"] = "val.year";
        if (f.PageCount is not null && (f.PageCount < 1 || f.PageCount > 20000)) errors["PageCount"] = "val.number";
        if (f.Description is { Length: > 4000 }) errors["Description"] = "val.toolong";
        if (genreIds.Count > CatalogService.MaxGenresPerBook) errors["Genres"] = "val.genres";
        if (errors.Count > 0) return null;

        var language = "ka";
        if (allowLanguage && !string.IsNullOrWhiteSpace(f.Language))
        {
            var l = f.Language.Trim().ToLowerInvariant();
            language = l.Length > 8 ? l[..8] : l;
        }
        return new ValidBook(title!, TextUtil.NullIfBlank(f.TitleEn), authors, isbn, TextUtil.NullIfBlank(f.Publisher),
            f.PublishYear, f.PageCount, language, TextUtil.NullIfBlank(f.Description), tags, genreIds);
    }

    /// <param name="tagsAsSubmitted">True for reader submissions: tags stay as plain text until a reviewer turns them into real tags.</param>
    public async Task<Book> CreateAsync(ValidBook v, string? coverPath, BookStatus status, Guid? submitterId, string source,
        bool tagsAsSubmitted, CancellationToken ct = default)
    {
        var book = new Book
        {
            Title = v.Title,
            TitleEn = v.TitleEn,
            Slug = await slugs.BookSlugAsync(v.Title, null, ct),
            Isbn = v.Isbn,
            Publisher = v.Publisher,
            PublishYear = v.Year,
            PageCount = v.Pages,
            Description = v.Description,
            CoverPath = coverPath,
            Language = v.Language,
            NormalizedKey = TextUtil.BookKey(v.Title, v.Authors[0]),
            Status = status,
            SubmittedById = submitterId,
            Source = source,
            SubmittedTags = tagsAsSubmitted && v.Tags.Count > 0 ? string.Join(", ", v.Tags) : null,
        };
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.Books.Add(book);
        await db.SaveChangesAsync(ct);
        await WriteRelationsAsync(book.Id, v, replaceAuthors: true, writeTags: !tagsAsSubmitted, ct);
        await tx.CommitAsync(ct);
        return book;
    }

    /// <summary>Applies the form to a tracked book. The caller sets status, cover and review fields beforehand if they change.</summary>
    public async Task UpdateAsync(Book book, ValidBook v, bool tagsAsSubmitted, CancellationToken ct = default)
    {
        var currentAuthors = await db.BookAuthors.Where(x => x.BookId == book.Id).OrderBy(x => x.SortOrder)
            .Select(x => x.Author.NormalizedName).ToListAsync(ct);
        var authorsChanged = !currentAuthors.SequenceEqual(v.Authors.Select(TextUtil.Key));

        book.Title = v.Title;
        book.TitleEn = v.TitleEn;
        book.Isbn = v.Isbn;
        book.Publisher = v.Publisher;
        book.PublishYear = v.Year;
        book.PageCount = v.Pages;
        book.Description = v.Description;
        book.Language = v.Language;
        book.NormalizedKey = TextUtil.BookKey(v.Title, v.Authors[0]);
        book.UpdatedAt = DateTime.UtcNow;
        book.SubmittedTags = tagsAsSubmitted && v.Tags.Count > 0 ? string.Join(", ", v.Tags) : null;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.SaveChangesAsync(ct);
        await WriteRelationsAsync(book.Id, v, authorsChanged, writeTags: !tagsAsSubmitted, ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>Fills a form from a stored book for editing.</summary>
    public async Task<BookForm> LoadFormAsync(Book book, CancellationToken ct = default)
    {
        var authors = await db.BookAuthors.AsNoTracking().Where(x => x.BookId == book.Id).OrderBy(x => x.SortOrder)
            .Select(x => x.Author.Name).ToListAsync(ct);
        var genres = await db.BookGenres.AsNoTracking().Where(x => x.BookId == book.Id).Select(x => x.GenreId).ToListAsync(ct);
        var tags = await db.BookTags.AsNoTracking().Where(x => x.BookId == book.Id).Select(x => x.Tag.Name).OrderBy(n => n).ToListAsync(ct);
        return new BookForm
        {
            Title = book.Title,
            TitleEn = book.TitleEn,
            Authors = string.Join("\n", authors),
            Isbn = book.Isbn,
            Publisher = book.Publisher,
            PublishYear = book.PublishYear,
            PageCount = book.PageCount,
            Language = book.Language,
            Description = book.Description,
            GenreIds = genres,
            // A reviewer sees the submitter's suggestions until real tags exist.
            Tags = tags.Count > 0 ? string.Join(", ", tags) : book.SubmittedTags,
            Status = book.Status,
        };
    }

    private async Task WriteRelationsAsync(Guid bookId, ValidBook v, bool replaceAuthors, bool writeTags, CancellationToken ct)
    {
        // Set-based deletes plus new rows (never a tracked collection), so re-adding the same author cannot trip EF's identity map.
        if (replaceAuthors)
        {
            await db.BookAuthors.Where(x => x.BookId == bookId).ExecuteDeleteAsync(ct);
            var order = 0;
            foreach (var name in v.Authors)
            {
                var author = await catalog.GetOrCreateAuthorAsync(name, ct);
                db.BookAuthors.Add(new BookAuthor { BookId = bookId, AuthorId = author.Id, SortOrder = order++ });
            }
            await db.SaveChangesAsync(ct);
        }
        await catalog.SetGenresAsync(bookId, v.GenreIds, ct);
        if (writeTags) await catalog.SetTagsAsync(bookId, v.Tags, ct);
    }
}
