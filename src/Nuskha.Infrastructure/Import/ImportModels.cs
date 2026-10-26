using System.Runtime.CompilerServices;

namespace Nuskha.Infrastructure.Import;

public record ImportedBook(
    string Title,
    string? Isbn,
    IReadOnlyList<string> Authors,
    string? Publisher,
    int? Year,
    int? Pages,
    string Source,
    string? ExternalId,
    string? SourceUrl);

public record ImportQuery(string? Text, int Limit);

public interface IBookSource
{
    string Id { get; }
    string DisplayName { get; }
    /// <summary>Disabled sources are listed in the admin panel but refuse to run.</summary>
    bool Enabled { get; }
    bool RequiresQuery { get; }
    string Notes { get; }
    IAsyncEnumerable<ImportedBook> FetchAsync(ImportQuery query, CancellationToken ct);
}

public class SiteOptions
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string BaseUrl { get; set; } = "";
    public string SitemapUrl { get; set; } = "";
    /// <summary>Regex a sitemap URL must match to be treated as a book page.</summary>
    public string? ProductUrlPattern { get; set; }
    /// <summary>Keep false until you have read the site's terms of use and robots.txt.</summary>
    public bool Enabled { get; set; }
    public int DelayMs { get; set; } = 2000;
}

public class ImportOptions
{
    public const string Section = "Import";
    public string UserAgent { get; set; } = "NuskhaBot/1.0 (+https://nuskha.ge)";
    public string? GoogleBooksApiKey { get; set; }
    public List<SiteOptions> Sites { get; set; } = [];
}
