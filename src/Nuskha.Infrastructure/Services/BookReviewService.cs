using Microsoft.EntityFrameworkCore;
using Nuskha.Domain;
using Nuskha.Domain.Entities;
using Nuskha.Infrastructure.Data;

namespace Nuskha.Infrastructure.Services;

/// <summary>
/// Moderation of user-submitted books. A submission is Pending until staff approve it (it then appears in the catalog)
/// or reject it with a reason (the submitter can see the reason, fix the book and resubmit).
/// </summary>
public class BookReviewService(NuskhaDbContext db, CatalogService catalog)
{
    public const int MaxNoteLength = 1000;
    public const int MaxPendingPerUser = 10;

    public async Task<bool> ApproveAsync(Guid bookId, Guid reviewerId, string? note, CancellationToken ct = default)
    {
        var book = await db.Books.FirstOrDefaultAsync(b => b.Id == bookId, ct);
        if (book is null || book.Status == BookStatus.Published) return false;

        // The reviewer has seen the submitter's tag suggestions; approving turns them into real tags (unless real tags already exist).
        if (!string.IsNullOrWhiteSpace(book.SubmittedTags))
        {
            if (!await db.BookTags.AnyAsync(t => t.BookId == book.Id, ct))
                await catalog.SetTagsAsync(book.Id, CatalogService.ParseTagNames(book.SubmittedTags), ct);
            book.SubmittedTags = null;
        }

        book.Status = BookStatus.Published;
        Stamp(book, reviewerId, note);
        await NotifySubmitterAsync(book, reviewerId, NotificationType.BookApproved, $"/books/{book.Slug}");
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Rejects a pending submission. A reason is required so the submitter knows what to fix.</summary>
    public async Task<bool> RejectAsync(Guid bookId, Guid reviewerId, string? reason, CancellationToken ct = default)
    {
        reason = TextUtil.NullIfBlank(reason);
        if (reason is null) return false;
        var book = await db.Books.FirstOrDefaultAsync(b => b.Id == bookId && b.Status == BookStatus.Pending, ct);
        if (book is null) return false;

        book.Status = BookStatus.Rejected;
        Stamp(book, reviewerId, reason);
        await NotifySubmitterAsync(book, reviewerId, NotificationType.BookRejected, "/books/mine");
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static void Stamp(Book book, Guid reviewerId, string? note)
    {
        note = TextUtil.NullIfBlank(note);
        if (note is { Length: > MaxNoteLength }) note = note[..MaxNoteLength];
        book.ReviewNote = note;
        book.ReviewedById = reviewerId;
        book.ReviewedAt = DateTime.UtcNow;
        book.UpdatedAt = DateTime.UtcNow;
    }

    private Task NotifySubmitterAsync(Book book, Guid reviewerId, NotificationType type, string url)
    {
        if (book.SubmittedById is Guid submitter && submitter != reviewerId)
            db.Notifications.Add(new Notification { UserId = submitter, ActorId = reviewerId, Type = type, Url = url });
        return Task.CompletedTask;
    }
}
