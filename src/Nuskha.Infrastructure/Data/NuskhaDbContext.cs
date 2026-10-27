using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Nuskha.Domain.Entities;

namespace Nuskha.Infrastructure.Data;

public class NuskhaDbContext(DbContextOptions<NuskhaDbContext> options)
    : IdentityDbContext<AppUser, AppRole, Guid>(options)
{
    /// <summary>ICU collation used to sort Georgian text correctly (EF.Functions.Collate).</summary>
    public const string GeorgianCollation = "ka_icu";

    public DbSet<Book> Books => Set<Book>();
    public DbSet<Author> Authors => Set<Author>();
    public DbSet<BookAuthor> BookAuthors => Set<BookAuthor>();
    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<BookGenre> BookGenres => Set<BookGenre>();
    public DbSet<BookTag> BookTags => Set<BookTag>();
    public DbSet<ImportRun> ImportRuns => Set<ImportRun>();
    public DbSet<ListEntry> ListEntries => Set<ListEntry>();
    public DbSet<Follow> Follows => Set<Follow>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Like> Likes => Set<Like>();
    public DbSet<ActivityEvent> Activities => Set<ActivityEvent>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<ForumCategory> ForumCategories => Set<ForumCategory>();
    public DbSet<ForumThread> ForumThreads => Set<ForumThread>();
    public DbSet<ForumPost> ForumPosts => Set<ForumPost>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasPostgresExtension("pg_trgm");
        builder.HasCollation(GeorgianCollation, locale: "ka-GE", provider: "icu", deterministic: true);
        builder.ApplyConfigurationsFromAssembly(typeof(NuskhaDbContext).Assembly);
    }
}
