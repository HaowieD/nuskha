using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Nuskha.Infrastructure.Services;

namespace Nuskha.Infrastructure.Import;

/// <summary>Google Books volumes API. Works without a key at low volume; set Import:GoogleBooksApiKey for more.</summary>
public sealed class GoogleBooksSource(IHttpClientFactory http, IOptions<ImportOptions> options) : IBookSource
{
    public string Id => "googlebooks";
    public string DisplayName => "Google Books";
    public bool Enabled => true;
    public bool RequiresQuery => true;
    public string Notes => "Requires a search text, e.g. an author name or 'inauthor:...'. Restricted to Georgian-language volumes.";

    public async IAsyncEnumerable<ImportedBook> FetchAsync(ImportQuery query, [EnumeratorCancellation] CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query.Text))
            throw new InvalidOperationException("Google Books needs a search text.");

        var client = http.CreateClient("import");
        var key = options.Value.GoogleBooksApiKey;
        var returned = 0;

        for (var start = 0; returned < query.Limit && start < 400; start += 40)
        {
            var url = "https://www.googleapis.com/books/v1/volumes?q=" + Uri.EscapeDataString(query.Text.Trim())
                      + $"&langRestrict=ka&printType=books&maxResults=40&startIndex={start}"
                      + (string.IsNullOrWhiteSpace(key) ? "" : "&key=" + Uri.EscapeDataString(key));
            using var resp = await client.GetAsync(url, ct);
            resp.EnsureSuccessStatusCode();
            using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            if (!doc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array || items.GetArrayLength() == 0)
                yield break;

            foreach (var item in items.EnumerateArray())
            {
                if (!item.TryGetProperty("volumeInfo", out var info)) continue;
                var title = info.Str("title");
                if (string.IsNullOrWhiteSpace(title)) continue;

                string? isbn = null;
                if (info.TryGetProperty("industryIdentifiers", out var ids) && ids.ValueKind == JsonValueKind.Array)
                    foreach (var idEl in ids.EnumerateArray())
                    {
                        isbn = IsbnHelper.Normalize(idEl.Str("identifier"));
                        if (isbn is not null) break;
                    }

                yield return new ImportedBook(
                    title, isbn, info.StrList("authors"), info.Str("publisher"),
                    JsonExt.YearFrom(info.Str("publishedDate")), info.Int("pageCount"),
                    Id, item.Str("id"), info.Str("infoLink"));
                if (++returned >= query.Limit) yield break;
            }
            await Task.Delay(500, ct);
        }
    }
}
