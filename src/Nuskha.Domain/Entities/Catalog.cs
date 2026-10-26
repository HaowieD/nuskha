namespace Nuskha.Domain.Entities;

public class Book
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    /// <summary>URL identifier: /books/{Slug}. Transliterated from the Georgian title, unique.</summary>
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string? TitleEn { get; set; }
    /// <summary>Normalized ISBN-13 (digits only) or null.</summary>
    public string? Isbn { get; set; }
    public string? Publisher { get; set; }
    public int? PublishYear { get; set; }
    public int? PageCount { get; set; }
    public string? Description { get; set; }
    public string? CoverPath { get; set; }
    public string Language { get; set; } = "ka";
    public BookStatus Status { get; set; } = BookStatus.Pending;

    public Guid? SubmittedById { get; set; }

    /// <summary>Provenance: which importer created the record and where it came from.</summary>
    public string? Source { get; set; }
    public string? ExternalId { get; set; }
    public string? SourceUrl { get; set; }

    /// <summary>Lower-cased letters/digits of title + first author; used for de-duplication.</summary>
    public string NormalizedKey { get; set; } = "";

    // Denormalized counters, maintained by ListService.
    public int ListCount { get; set; }
    public int RatingCount { get; set; }
    public int RatingSum { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<BookAuthor> Authors { get; set; } = [];

    public double? AverageRating => RatingCount == 0 ? null : (double)RatingSum / RatingCount;
}

public class Author
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string? NameEn { get; set; }
    public string NormalizedName { get; set; } = "";
    public string? Bio { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<BookAuthor> Books { get; set; } = [];
}

public class BookAuthor
{
    public Guid BookId { get; set; }
    public Book Book { get; set; } = null!;
    public Guid AuthorId { get; set; }
    public Author Author { get; set; } = null!;
    public int SortOrder { get; set; }
}

public class ImportRun
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Source { get; set; } = "";
    public string? Query { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }
    public int Added { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public string? Error { get; set; }
    public Guid? TriggeredById { get; set; }
}
