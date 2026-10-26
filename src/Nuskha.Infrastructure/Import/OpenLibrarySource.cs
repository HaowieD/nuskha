using System.Runtime.CompilerServices;
using System.Text.Json;
using Nuskha.Infrastructure.Services;

namespace Nuskha.Infrastructure.Import;

/// <summary>Open Library search API (public, no key). Georgian = language code "geo".</summary>
public sealed class OpenLibrarySource(IHttpClientFactory http) : IBookSource
{
    public string Id => "openlibrary";
    public string DisplayName => "Open Library";
    public bool Enabled => true;
    public bool RequiresQuery => false;
    public string Notes => "Public API. Coverage of Georgian titles is partial; best used with author or title queries.";

    public async IAsyncEnumerable<ImportedBook> FetchAsync(ImportQuery query, [EnumeratorCancellation] CancellationToken ct)
    {
        var client = http.CreateClient("import");
        var text = string.IsNullOrWhiteSpace(query.Text) ? "language:geo" : $"({query.Text.Trim()}) AND language:geo";
        var returned = 0;

        for (var page = 1; returned < query.Limit; page++)
        {
            var url = "https://openlibrary.org/search.json?q=" + Uri.EscapeDataString(text)
                      + "&fields=key,title,author_name,isbn,publisher,first_publish_year,number_of_pages_median"
                      + $"&limit=100&page={page}";
            using var resp = await client.GetAsync(url, ct);
            resp.EnsureSuccessStatusCode();
            using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            if (!doc.RootElement.TryGetProperty("docs", out var docs) || docs.ValueKind != JsonValueKind.Array || docs.GetArrayLength() == 0)
                yield break;

            foreach (var d in docs.EnumerateArray())
            {
                var title = d.Str("title");
                if (string.IsNullOrWhiteSpace(title)) continue;
                var isbn = d.StrList("isbn").Select(IsbnHelper.Normalize).FirstOrDefault(i => i is not null);
                var key = d.Str("key");
                yield return new ImportedBook(
                    title, isbn, d.StrList("author_name"), d.StrList("publisher").FirstOrDefault(),
                    d.Int("first_publish_year"), d.Int("number_of_pages_median"),
                    Id, key, key is null ? null : "https://openlibrary.org" + key);
                if (++returned >= query.Limit) yield break;
            }
            await Task.Delay(1000, ct); // be polite to a free public API
        }
    }
}
