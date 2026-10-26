using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using HtmlAgilityPack;
using Nuskha.Infrastructure.Services;

namespace Nuskha.Infrastructure.Import;

/// <summary>
/// Structure-agnostic importer for shop/catalog websites. It reads the site's sitemap, visits the book pages
/// that match <see cref="SiteOptions.ProductUrlPattern"/> and extracts schema.org JSON-LD (Product / Book)
/// or Open Graph book:isbn metadata. It honours robots.txt and rate-limits itself.
/// A site is only crawled when its <c>Enabled</c> flag is set in configuration.
/// </summary>
public sealed class SitemapJsonLdSource(SiteOptions site, IHttpClientFactory http, string botToken) : IBookSource
{
    public string Id => site.Id;
    public string DisplayName => site.DisplayName;
    public bool Enabled => site.Enabled;
    public bool RequiresQuery => false;
    public string Notes => "Crawls the sitemap, honours robots.txt, extracts JSON-LD. Enable only after reviewing the site's terms of use.";

    public async IAsyncEnumerable<ImportedBook> FetchAsync(ImportQuery query, [EnumeratorCancellation] CancellationToken ct)
    {
        var client = http.CreateClient("import");
        var baseUri = new Uri(site.BaseUrl);

        var robots = await LoadRobotsAsync(client, baseUri, ct);
        var delay = TimeSpan.FromMilliseconds(Math.Max(site.DelayMs, robots.CrawlDelaySeconds * 1000));

        var sitemapUrl = string.IsNullOrWhiteSpace(site.SitemapUrl) ? new Uri(baseUri, "/sitemap.xml").ToString() : site.SitemapUrl;
        if (!robots.IsAllowed(new Uri(sitemapUrl).PathAndQuery))
            throw new InvalidOperationException("robots.txt disallows the sitemap.");

        var pattern = string.IsNullOrWhiteSpace(site.ProductUrlPattern) ? null : new Regex(site.ProductUrlPattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        var urls = await CollectSitemapUrlsAsync(client, sitemapUrl, 0, delay, ct);
        var candidates = urls.Where(u => pattern is null || pattern.IsMatch(u)).Distinct().ToList();

        var visited = 0;
        foreach (var url in candidates)
        {
            if (visited >= query.Limit) yield break;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Host != baseUri.Host) continue;
            if (!robots.IsAllowed(uri.PathAndQuery)) continue;

            visited++;
            await Task.Delay(delay, ct);
            string html;
            using (var resp = await client.GetAsync(uri, ct))
            {
                if (resp.StatusCode == HttpStatusCode.TooManyRequests) throw new InvalidOperationException("Site rate-limited us (429); stopping.");
                if (!resp.IsSuccessStatusCode) continue;
                html = await resp.Content.ReadAsStringAsync(ct);
            }

            var book = Extract(html, url);
            if (book is not null) yield return book;
        }
    }

    /// <summary>Outcome of <see cref="PreviewAsync"/>: what a single page would yield, for the admin "check a page" tool.</summary>
    public sealed record PagePreview(string? ErrorKey, int? Status, ImportedBook? Book, IReadOnlyList<string> JsonLdTypes);

    /// <summary>
    /// Fetches one page of this site (obeying robots.txt) and reports what the extractor finds, so an admin can
    /// judge whether the site is usable before enabling a crawl. Only URLs on the configured host are fetched.
    /// </summary>
    public async Task<PagePreview> PreviewAsync(string url, CancellationToken ct)
    {
        var baseUri = new Uri(site.BaseUrl);
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")
            || !string.Equals(uri.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase))
            return new("admin.import.check.host", null, null, []);

        var client = http.CreateClient("import");
        try
        {
            var robots = await LoadRobotsAsync(client, baseUri, ct);
            if (!robots.IsAllowed(uri.PathAndQuery)) return new("admin.import.check.robots", null, null, []);

            using var resp = await client.GetAsync(uri, ct);
            var status = (int)resp.StatusCode;
            // A redirect to another host must not be read: the check is only meant for the site's own pages.
            if (!string.Equals(resp.RequestMessage?.RequestUri?.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase))
                return new("admin.import.check.host", status, null, []);
            if (!resp.IsSuccessStatusCode) return new("admin.import.check.fetch", status, null, []);

            var html = await resp.Content.ReadAsStringAsync(ct);
            return new(null, status, Extract(html, uri.ToString()), ListJsonLdTypes(html));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return new("admin.import.check.fetch", null, null, []);
        }
    }

    private static List<string> ListJsonLdTypes(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var types = new HashSet<string>();
        void Collect(JsonElement e)
        {
            if (e.ValueKind == JsonValueKind.Array) { foreach (var x in e.EnumerateArray()) Collect(x); return; }
            if (e.ValueKind != JsonValueKind.Object) return;
            if (e.TryGetProperty("@type", out var t))
            {
                if (t.ValueKind == JsonValueKind.String && t.GetString() is { } one) types.Add(one);
                else if (t.ValueKind == JsonValueKind.Array)
                    foreach (var x in t.EnumerateArray()) if (x.GetString() is { } many) types.Add(many);
            }
            if (e.TryGetProperty("@graph", out var g)) Collect(g);
        }
        foreach (var node in doc.DocumentNode.SelectNodes("//script[@type='application/ld+json']") ?? Enumerable.Empty<HtmlNode>())
        {
            try
            {
                using var j = JsonDocument.Parse(WebUtility.HtmlDecode(node.InnerText.Trim()));
                Collect(j.RootElement);
            }
            catch (JsonException) { /* malformed block: ignore */ }
        }
        return types.Order().ToList();
    }

    private async Task<RobotsRules> LoadRobotsAsync(HttpClient client, Uri baseUri, CancellationToken ct)
    {
        using var resp = await client.GetAsync(new Uri(baseUri, "/robots.txt"), ct);
        if (resp.StatusCode == HttpStatusCode.NotFound) return RobotsRules.AllowAll();
        // If we cannot read the rules we do not crawl: refusing is the safe default.
        resp.EnsureSuccessStatusCode();
        return RobotsRules.Parse(await resp.Content.ReadAsStringAsync(ct), botToken);
    }

    private static async Task<List<string>> CollectSitemapUrlsAsync(HttpClient client, string url, int depth, TimeSpan delay, CancellationToken ct)
    {
        var result = new List<string>();
        if (depth > 2) return result;
        using var resp = await client.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();
        var xml = XDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var root = xml.Root;
        if (root is null) return result;

        var locs = root.Descendants().Where(e => e.Name.LocalName == "loc").Select(e => e.Value.Trim()).Where(v => v.Length > 0).ToList();
        if (root.Name.LocalName == "sitemapindex")
        {
            foreach (var child in locs.Take(50))
            {
                await Task.Delay(delay, ct);
                result.AddRange(await CollectSitemapUrlsAsync(client, child, depth + 1, delay, ct));
            }
        }
        else result.AddRange(locs);
        return result;
    }

    private ImportedBook? Extract(string html, string url)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        foreach (var node in doc.DocumentNode.SelectNodes("//script[@type='application/ld+json']") ?? Enumerable.Empty<HtmlNode>())
        {
            foreach (var obj in ParseJsonLd(node.InnerText))
            {
                var title = Clean(obj.Str("name"));
                if (title is null) continue;
                var isbn = new[] { obj.Str("isbn"), obj.Str("gtin13"), obj.Str("gtin"), obj.Str("gtin14"), obj.Str("sku") }
                    .Select(IsbnHelper.Normalize).FirstOrDefault(i => i is not null);
                var authors = Names(obj, "author");
                var publisher = Names(obj, "publisher").FirstOrDefault() ?? Names(obj, "brand").FirstOrDefault();
                return new ImportedBook(title, isbn, authors, publisher,
                    JsonExt.YearFrom(obj.Str("datePublished")), obj.Int("numberOfPages"),
                    Id, new Uri(url).AbsolutePath, url);
            }
        }

        // Fallback: Open Graph book metadata.
        var ogTitle = doc.DocumentNode.SelectSingleNode("//meta[@property='og:title']")?.GetAttributeValue("content", null);
        var ogIsbn = doc.DocumentNode.SelectSingleNode("//meta[@property='book:isbn']")?.GetAttributeValue("content", null);
        var ogAuthor = doc.DocumentNode.SelectSingleNode("//meta[@property='book:author']")?.GetAttributeValue("content", null);
        if (Clean(ogTitle) is { } t && IsbnHelper.Normalize(ogIsbn) is { } isbn13)
            return new ImportedBook(t, isbn13, ogAuthor is null ? [] : [ogAuthor], null, null, null, Id, new Uri(url).AbsolutePath, url);
        return null;
    }

    private static List<JsonElement> ParseJsonLd(string raw)
    {
        var found = new List<JsonElement>();
        try
        {
            using var doc = JsonDocument.Parse(WebUtility.HtmlDecode(raw.Trim()));
            Walk(doc.RootElement.Clone(), found);
        }
        catch (JsonException) { /* malformed block: ignore */ }
        return found;
    }

    private static void Walk(JsonElement e, List<JsonElement> found)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var x in e.EnumerateArray()) Walk(x, found);
                break;
            case JsonValueKind.Object:
                if (e.TryGetProperty("@graph", out var graph)) Walk(graph, found);
                if (e.TryGetProperty("@type", out var type))
                {
                    IEnumerable<string> types = type.ValueKind == JsonValueKind.Array
                        ? type.EnumerateArray().Select(t => t.GetString() ?? "")
                        : new[] { type.GetString() ?? "" };
                    if (types.Any(t => t is "Product" or "Book")) found.Add(e);
                }
                break;
        }
    }

    private static List<string> Names(JsonElement obj, string prop)
    {
        var names = new List<string>();
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(prop, out var v)) return names;
        void Add(JsonElement x)
        {
            if (x.ValueKind == JsonValueKind.String) { if (Clean(x.GetString()) is { } s) names.Add(s); }
            else if (x.ValueKind == JsonValueKind.Object && Clean(x.Str("name")) is { } n) names.Add(n);
        }
        if (v.ValueKind == JsonValueKind.Array) foreach (var x in v.EnumerateArray()) Add(x);
        else Add(v);
        return names.Distinct().Take(5).ToList();
    }

    private static string? Clean(string? s)
    {
        s = WebUtility.HtmlDecode(s ?? "").Trim();
        return s.Length == 0 ? null : s;
    }
}
