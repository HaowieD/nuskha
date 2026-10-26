using Microsoft.EntityFrameworkCore;
using Nuskha.Domain;
using Nuskha.Domain.Entities;
using Nuskha.Infrastructure.Data;

namespace Nuskha.Infrastructure.Services;

public record ListEntryInput(
    ReadingStatus Status, int? Rating, int? PagesRead,
    DateOnly? StartedOn, DateOnly? FinishedOn, string? Notes, bool IsPrivate);

public class ListService(NuskhaDbContext db)
{
    public async Task<ListEntry?> GetAsync(Guid userId, Guid bookId, CancellationToken ct = default) =>
        await db.ListEntries.AsNoTracking().FirstOrDefaultAsync(e => e.UserId == userId && e.BookId == bookId, ct);

    public async Task<ListEntry?> UpsertAsync(Guid userId, Guid bookId, ListEntryInput input, CancellationToken ct = default)
    {
        var bookOk = await db.Books.AnyAsync(b => b.Id == bookId && b.Status == BookStatus.Published, ct);
        if (!bookOk) return null;

        var rating = input.Rating is >= 1 and <= 10 ? input.Rating : null;
        var pages = input.PagesRead is >= 0 ? input.PagesRead : null;
        var notes = TextUtil.NullIfBlank(input.Notes);
        if (notes is { Length: > 2000 }) notes = notes[..2000];

        var entry = await db.ListEntries.FirstOrDefaultAsync(e => e.UserId == userId && e.BookId == bookId, ct);
        var isNew = entry is null;
        var oldStatus = entry?.Status;
        var oldRating = entry?.Rating;

        entry ??= db.ListEntries.Add(new ListEntry { UserId = userId, BookId = bookId }).Entity;
        entry.Status = input.Status;
        entry.Rating = rating;
        entry.PagesRead = pages;
        entry.StartedOn = input.StartedOn;
        entry.FinishedOn = input.Status == ReadingStatus.Completed ? input.FinishedOn ?? DateOnly.FromDateTime(DateTime.UtcNow) : input.FinishedOn;
        entry.Notes = notes;
        entry.IsPrivate = input.IsPrivate;
        entry.UpdatedAt = DateTime.UtcNow;

        if (!entry.IsPrivate)
        {
            // One activity per change; the most meaningful one wins so the feed is not spammed.
            ActivityEvent? ev = null;
            if (input.Status == ReadingStatus.Completed && oldStatus != ReadingStatus.Completed)
                ev = Event(userId, bookId, ActivityType.Completed, rating?.ToString());
            else if (rating is not null && rating != oldRating)
                ev = Event(userId, bookId, ActivityType.Rated, rating.ToString());
            else if (isNew)
                ev = Event(userId, bookId, ActivityType.AddedToList, input.Status.ToString());
            else if (oldStatus != input.Status)
                ev = Event(userId, bookId, ActivityType.StatusChanged, input.Status.ToString());
            if (ev is not null) db.Activities.Add(ev);
        }

        await db.SaveChangesAsync(ct);
        await RecomputeBookStatsAsync(bookId, ct);
        return entry;
    }

    public async Task RemoveAsync(Guid userId, Guid bookId, CancellationToken ct = default)
    {
        var deleted = await db.ListEntries.Where(e => e.UserId == userId && e.BookId == bookId).ExecuteDeleteAsync(ct);
        if (deleted > 0) await RecomputeBookStatsAsync(bookId, ct);
    }

    public async Task RecomputeBookStatsAsync(Guid bookId, CancellationToken ct = default)
    {
        var count = await db.ListEntries.CountAsync(e => e.BookId == bookId, ct);
        var ratingCount = await db.ListEntries.CountAsync(e => e.BookId == bookId && e.Rating != null, ct);
        var ratingSum = await db.ListEntries.Where(e => e.BookId == bookId && e.Rating != null).SumAsync(e => e.Rating ?? 0, ct);
        await db.Books.Where(b => b.Id == bookId).ExecuteUpdateAsync(s => s
            .SetProperty(b => b.ListCount, count)
            .SetProperty(b => b.RatingCount, ratingCount)
            .SetProperty(b => b.RatingSum, ratingSum), ct);
    }

    private static ActivityEvent Event(Guid userId, Guid bookId, ActivityType type, string? data) =>
        new() { UserId = userId, BookId = bookId, Type = type, Data = data };
}
