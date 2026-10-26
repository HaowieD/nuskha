using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nuskha.Domain;
using Nuskha.Domain.Entities;
using Nuskha.Infrastructure.Data;
using Nuskha.Infrastructure.Services;

namespace Nuskha.Infrastructure.Import;

public class ImportService(NuskhaDbContext db, SlugService slugs, CatalogService catalog, BookSourceRegistry registry, ILogger<ImportService> log)
{
    private enum Outcome { Added, Updated, Skipped }

    public async Task RunAsync(string sourceId, ImportQuery query, Guid? triggeredBy, CancellationToken ct)
    {
        var run = new ImportRun { Source = sourceId, Query = query.Text, TriggeredById = triggeredBy };
        db.ImportRuns.Add(run);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();

        int added = 0, updated = 0, skipped = 0;
        string? error = null;
        try
        {
            var source = registry.Find(sourceId) ?? throw new InvalidOperationException($"Unknown source '{sourceId}'.");
            if (!source.Enabled) throw new InvalidOperationException("This source is disabled in configuration.");

            var processed = 0;
            await foreach (var item in source.FetchAsync(query, ct))
            {
                try
                {
                    switch (await UpsertAsync(item, ct))
                    {
                        case Outcome.Added: added++; break;
                        case Outcome.Updated: updated++; break;
                        default: skipped++; break;
                    }
                }
                catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException)
                {
                    skipped++;
                    log.LogWarning(ex, "Skipping imported book '{Title}'", item.Title);
                }
                finally { db.ChangeTracker.Clear(); }

                if (++processed % 25 == 0) await SaveProgressAsync(run.Id, added, updated, skipped, ct);
            }
        }
        catch (OperationCanceledException) { error = "Cancelled"; }
        catch (Exception ex)
        {
            log.LogError(ex, "Import from {Source} failed", sourceId);
            error = ex.Message.Length > 1900 ? ex.Message[..1900] : ex.Message;
        }

        await db.ImportRuns.Where(r => r.Id == run.Id).ExecuteUpdateAsync(s => s
            .SetProperty(r => r.Added, added).SetProperty(r => r.Updated, updated).SetProperty(r => r.Skipped, skipped)
            .SetProperty(r => r.FinishedAt, DateTime.UtcNow).SetProperty(r => r.Error, error), CancellationToken.None);
    }

    private Task<int> SaveProgressAsync(Guid runId, int added, int updated, int skipped, CancellationToken ct) =>
        db.ImportRuns.Where(r => r.Id == runId).ExecuteUpdateAsync(s => s
            .SetProperty(r => r.Added, added).SetProperty(r => r.Updated, updated).SetProperty(r => r.Skipped, skipped), ct);

    private async Task<Outcome> UpsertAsync(ImportedBook item, CancellationToken ct)
    {
        var title = item.Title.Trim();
        if (title.Length == 0) return Outcome.Skipped;
        if (title.Length > 300) title = title[..300];

        var isbn = IsbnHelper.Normalize(item.Isbn);
        var authors = item.Authors.Select(a => a.Trim()).Where(a => a.Length > 0).Select(a => a.Length > 200 ? a[..200] : a).Distinct().Take(5).ToList();
        var key = TextUtil.BookKey(title, authors.FirstOrDefault());

        Book? existing = null;
        if (isbn is not null)
            existing = await db.Books.FirstOrDefaultAsync(b => b.Isbn == isbn, ct);
        if (existing is null && item.ExternalId is not null)
            existing = await db.Books.FirstOrDefaultAsync(b => b.Source == item.Source && b.ExternalId == item.ExternalId, ct);
        existing ??= await db.Books.FirstOrDefaultAsync(b => b.NormalizedKey == key, ct);

        if (existing is not null)
        {
            // Never overwrite what editors may have curated; only fill gaps.
            var changed = false;
            if (existing.Isbn is null && isbn is not null) { existing.Isbn = isbn; changed = true; }
            if (existing.Publisher is null && item.Publisher is not null) { existing.Publisher = Trim(item.Publisher, 200); changed = true; }
            if (existing.PublishYear is null && item.Year is not null) { existing.PublishYear = item.Year; changed = true; }
            if (existing.PageCount is null && item.Pages is > 0) { existing.PageCount = item.Pages; changed = true; }
            if (existing.Source is null) { existing.Source = item.Source; existing.ExternalId = Trim(item.ExternalId, 200); existing.SourceUrl = Trim(item.SourceUrl, 500); changed = true; }
            if (!changed) return Outcome.Skipped;
            existing.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return Outcome.Updated;
        }

        var book = new Book
        {
            Title = title,
            Slug = await slugs.BookSlugAsync(title, null, ct),
            Isbn = isbn,
            Publisher = Trim(item.Publisher, 200),
            PublishYear = item.Year,
            PageCount = item.Pages is > 0 ? item.Pages : null,
            Source = item.Source,
            ExternalId = Trim(item.ExternalId, 200),
            SourceUrl = Trim(item.SourceUrl, 500),
            NormalizedKey = key,
            Status = BookStatus.Published,
        };
        var order = 0;
        foreach (var name in authors)
        {
            var author = await catalog.GetOrCreateAuthorAsync(name, ct);
            book.Authors.Add(new BookAuthor { Author = author, SortOrder = order++ });
        }
        db.Books.Add(book);
        await db.SaveChangesAsync(ct);
        return Outcome.Added;
    }

    private static string? Trim(string? s, int max) => string.IsNullOrWhiteSpace(s) ? null : (s.Length > max ? s[..max] : s.Trim());
}
