using Microsoft.Extensions.Options;

namespace Nuskha.Infrastructure.Import;

public class BookSourceRegistry
{
    public IReadOnlyList<IBookSource> All { get; }

    public BookSourceRegistry(OpenLibrarySource openLibrary, GoogleBooksSource googleBooks,
        IOptions<ImportOptions> options, IHttpClientFactory http)
    {
        var list = new List<IBookSource> { openLibrary, googleBooks };
        var botToken = options.Value.UserAgent.Split('/')[0];
        foreach (var site in options.Value.Sites.Where(s => !string.IsNullOrWhiteSpace(s.Id) && !string.IsNullOrWhiteSpace(s.BaseUrl)))
            list.Add(new SitemapJsonLdSource(site, http, botToken));
        All = list;
    }

    public IBookSource? Find(string id) => All.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));
}
