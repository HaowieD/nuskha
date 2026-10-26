using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nuskha.Domain.Entities;

namespace Nuskha.Infrastructure.Data.Configurations;

internal sealed class ForumCategoryConfiguration : IEntityTypeConfiguration<ForumCategory>
{
    public void Configure(EntityTypeBuilder<ForumCategory> b)
    {
        b.ToTable("ForumCategories");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Slug).HasMaxLength(80).IsRequired();
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.NameEn).HasMaxLength(120);
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.DescriptionEn).HasMaxLength(500);
        b.HasIndex(x => x.Slug).IsUnique();
    }
}

internal sealed class ForumThreadConfiguration : IEntityTypeConfiguration<ForumThread>
{
    public void Configure(EntityTypeBuilder<ForumThread> b)
    {
        b.ToTable("ForumThreads");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Slug).HasMaxLength(200).IsRequired();
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.HasOne(x => x.Category).WithMany(x => x.Threads).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.Slug).IsUnique();
        b.HasIndex(x => new { x.CategoryId, x.IsPinned, x.LastPostAt });
    }
}

internal sealed class ForumPostConfiguration : IEntityTypeConfiguration<ForumPost>
{
    public void Configure(EntityTypeBuilder<ForumPost> b)
    {
        b.ToTable("ForumPosts");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Body).HasMaxLength(10000).IsRequired();
        b.HasOne(x => x.Thread).WithMany(x => x.Posts).HasForeignKey(x => x.ThreadId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ThreadId, x.CreatedAt });
    }
}
