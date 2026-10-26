using Nuskha.Domain.Entities;
using Nuskha.Web.Models;

namespace Nuskha.Web.Services;

public static class BookQueries
{
    public static IQueryable<BookListItem> ToListItems(this IQueryable<Book> books) =>
        books.Select(b => new BookListItem
        {
            Id = b.Id,
            Slug = b.Slug,
            Title = b.Title,
            TitleEn = b.TitleEn,
            CoverPath = b.CoverPath,
            PublishYear = b.PublishYear,
            ListCount = b.ListCount,
            RatingCount = b.RatingCount,
            RatingSum = b.RatingSum,
            Authors = b.Authors.OrderBy(a => a.SortOrder)
                .Select(a => new AuthorRef(a.Author.Slug, a.Author.Name, a.Author.NameEn)).ToList(),
        });
}
