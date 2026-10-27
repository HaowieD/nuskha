using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nuskha.Domain.Entities;

namespace Nuskha.Infrastructure.Data.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> b)
    {
        b.Property(x => x.DisplayName).HasMaxLength(60).IsRequired();
        b.Property(x => x.Bio).HasMaxLength(2000);
        b.Property(x => x.AvatarPath).HasMaxLength(300);
        b.Property(x => x.BannerPath).HasMaxLength(300);
        b.HasIndex(x => x.CreatedAt);
    }
}

internal sealed class ListEntryConfiguration : IEntityTypeConfiguration<ListEntry>
{
    public void Configure(EntityTypeBuilder<ListEntry> b)
    {
        b.ToTable("ListEntries", t => t.HasCheckConstraint("CK_ListEntries_Rating", "\"Rating\" IS NULL OR (\"Rating\" BETWEEN 1 AND 10)"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Notes).HasMaxLength(2000);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Book).WithMany().HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.UserId, x.BookId }).IsUnique();
        b.HasIndex(x => new { x.UserId, x.Status });
        b.HasIndex(x => x.BookId);
    }
}

internal sealed class FollowConfiguration : IEntityTypeConfiguration<Follow>
{
    public void Configure(EntityTypeBuilder<Follow> b)
    {
        b.ToTable("Follows", t => t.HasCheckConstraint("CK_Follows_NotSelf", "\"FollowerId\" <> \"FolloweeId\""));
        b.HasKey(x => new { x.FollowerId, x.FolloweeId });
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.FollowerId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.FolloweeId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.FolloweeId);
    }
}

internal sealed class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> b)
    {
        b.ToTable("Comments");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Body).HasMaxLength(2000).IsRequired();
        b.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.TargetType, x.TargetId, x.CreatedAt });
        b.HasIndex(x => x.AuthorId);
    }
}

internal sealed class LikeConfiguration : IEntityTypeConfiguration<Like>
{
    public void Configure(EntityTypeBuilder<Like> b)
    {
        b.ToTable("Likes");
        b.HasKey(x => new { x.UserId, x.TargetType, x.TargetId });
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.TargetType, x.TargetId });
    }
}

internal sealed class ActivityConfiguration : IEntityTypeConfiguration<ActivityEvent>
{
    public void Configure(EntityTypeBuilder<ActivityEvent> b)
    {
        b.ToTable("Activities");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Data).HasMaxLength(100);
        b.Property(x => x.Body).HasMaxLength(3000);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Book).WithMany().HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Thread).WithMany().HasForeignKey(x => x.ThreadId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Recipient).WithMany().HasForeignKey(x => x.RecipientId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.UserId, x.CreatedAt });
        b.HasIndex(x => new { x.RecipientId, x.CreatedAt });
        b.HasIndex(x => x.CreatedAt);
    }
}

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("Notifications");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Url).HasMaxLength(500).IsRequired();
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Actor).WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAt });
    }
}

internal sealed class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> b)
    {
        b.ToTable("Reports");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        b.HasOne(x => x.Reporter).WithMany().HasForeignKey(x => x.ReporterId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.Status, x.CreatedAt });
    }
}
