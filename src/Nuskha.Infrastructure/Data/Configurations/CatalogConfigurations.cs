using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nuskha.Domain.Entities;

namespace Nuskha.Infrastructure.Data.Configurations;

internal sealed class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> b)
    {
        b.ToTable("Books");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Slug).HasMaxLength(200).IsRequired();
        b.Property(x => x.Title).HasMaxLength(300).IsRequired();
        b.Property(x => x.TitleEn).HasMaxLength(300);
        b.Property(x => x.Isbn).HasMaxLength(13);
        b.Property(x => x.Publisher).HasMaxLength(200);
        b.Property(x => x.Description).HasMaxLength(4000);
        b.Property(x => x.CoverPath).HasMaxLength(300);
        b.Property(x => x.Language).HasMaxLength(8).IsRequired();
        b.Property(x => x.Source).HasMaxLength(64);
        b.Property(x => x.ExternalId).HasMaxLength(200);
        b.Property(x => x.SourceUrl).HasMaxLength(500);
        b.Property(x => x.NormalizedKey).HasMaxLength(500).IsRequired();
        b.Property(x => x.ReviewNote).HasMaxLength(1000);
        b.Property(x => x.SubmittedTags).HasMaxLength(500);
        b.Ignore(x => x.AverageRating);

        b.HasIndex(x => x.Slug).IsUnique();
        b.HasIndex(x => x.Isbn).IsUnique().HasFilter("\"Isbn\" IS NOT NULL");
        b.HasIndex(x => new { x.Source, x.ExternalId }).IsUnique().HasFilter("\"ExternalId\" IS NOT NULL");
        b.HasIndex(x => x.NormalizedKey);
        b.HasIndex(x => new { x.Status, x.ListCount });
        b.HasIndex(x => x.CreatedAt);
        b.HasIndex(x => new { x.Status, x.CreatedAt });
        // Fuzzy / substring search on titles.
        b.HasIndex(x => x.Title).HasMethod("gin").HasOperators("gin_trgm_ops").HasDatabaseName("IX_Books_Title_Trgm");

        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.SubmittedById).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class AuthorConfiguration : IEntityTypeConfiguration<Author>
{
    public void Configure(EntityTypeBuilder<Author> b)
    {
        b.ToTable("Authors");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Slug).HasMaxLength(200).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.NameEn).HasMaxLength(200);
        b.Property(x => x.NormalizedName).HasMaxLength(300).IsRequired();
        b.Property(x => x.Bio).HasMaxLength(4000);
        b.HasIndex(x => x.Slug).IsUnique();
        b.HasIndex(x => x.NormalizedName);
        b.HasIndex(x => x.Name).HasMethod("gin").HasOperators("gin_trgm_ops").HasDatabaseName("IX_Authors_Name_Trgm");
    }
}

internal sealed class BookAuthorConfiguration : IEntityTypeConfiguration<BookAuthor>
{
    public void Configure(EntityTypeBuilder<BookAuthor> b)
    {
        b.ToTable("BookAuthors");
        b.HasKey(x => new { x.BookId, x.AuthorId });
        b.HasOne(x => x.Book).WithMany(x => x.Authors).HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Author).WithMany(x => x.Books).HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.AuthorId);
    }
}

internal sealed class ImportRunConfiguration : IEntityTypeConfiguration<ImportRun>
{
    public void Configure(EntityTypeBuilder<ImportRun> b)
    {
        b.ToTable("ImportRuns");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Source).HasMaxLength(64).IsRequired();
        b.Property(x => x.Query).HasMaxLength(300);
        b.Property(x => x.Error).HasMaxLength(2000);
        b.HasIndex(x => x.StartedAt);
    }
}

internal sealed class GenreConfiguration : IEntityTypeConfiguration<Genre>
{
    public void Configure(EntityTypeBuilder<Genre> b)
    {
        b.ToTable("Genres");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Slug).HasMaxLength(80).IsRequired();
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.NameEn).HasMaxLength(100);
        b.HasIndex(x => x.Slug).IsUnique();
        b.HasIndex(x => x.SortOrder);
    }
}

internal sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> b)
    {
        b.ToTable("Tags");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Slug).HasMaxLength(80).IsRequired();
        b.Property(x => x.Name).HasMaxLength(60).IsRequired();
        b.Property(x => x.NormalizedName).HasMaxLength(120).IsRequired();
        b.HasIndex(x => x.Slug).IsUnique();
        b.HasIndex(x => x.NormalizedName).IsUnique();
    }
}

internal sealed class BookGenreConfiguration : IEntityTypeConfiguration<BookGenre>
{
    public void Configure(EntityTypeBuilder<BookGenre> b)
    {
        b.ToTable("BookGenres");
        b.HasKey(x => new { x.BookId, x.GenreId });
        b.HasOne(x => x.Book).WithMany(x => x.Genres).HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Genre).WithMany(x => x.Books).HasForeignKey(x => x.GenreId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.GenreId);
    }
}

internal sealed class BookTagConfiguration : IEntityTypeConfiguration<BookTag>
{
    public void Configure(EntityTypeBuilder<BookTag> b)
    {
        b.ToTable("BookTags");
        b.HasKey(x => new { x.BookId, x.TagId });
        b.HasOne(x => x.Book).WithMany(x => x.Tags).HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Tag).WithMany(x => x.Books).HasForeignKey(x => x.TagId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.TagId);
    }
}
