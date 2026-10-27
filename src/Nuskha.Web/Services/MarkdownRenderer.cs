using Markdig;
using Markdig.Parsers.Inlines;
using Markdig.Renderers;
using Markdig.Renderers.Html.Inlines;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.AspNetCore.Html;

namespace Nuskha.Web.Services;

public interface IMarkdown
{
    /// <summary>Turns user-written markup into safe HTML: raw HTML is escaped, images are dropped, only http(s)/mailto/relative links survive.</summary>
    IHtmlContent Render(string? text);
}

/// <summary>
/// User text is stored as plain markup and rendered on the way out, so a bug here can never be baked into the database.
/// Supported: **bold**, *italic*, ~~strike~~, `code`, ||spoiler||, [links](https://...), lists, quotes, headings, code blocks.
/// </summary>
public sealed class MarkdownRenderer : IMarkdown
{
    private const int MaxChars = 20_000;

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .DisableHtml()
        .UseAutoLinks()
        .UseEmphasisExtras(Markdig.Extensions.EmphasisExtras.EmphasisExtraOptions.Strikethrough)
        .UseSoftlineBreakAsHardlineBreak()
        .Use<SpoilerExtension>()
        .Build();

    public IHtmlContent Render(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return HtmlString.Empty;
        if (text.Length > MaxChars) text = text[..MaxChars];

        var document = Markdown.Parse(text, Pipeline);
        Sanitize(document);
        return new HtmlString(Markdown.ToHtml(document, Pipeline));
    }

    private static void Sanitize(MarkdownDocument document)
    {
        foreach (var link in document.Descendants<LinkInline>().ToList())
        {
            // Remote images would leak every reader's IP to a third party and are blocked by our CSP anyway.
            if (link.IsImage) link.IsImage = false;
            if (!IsSafeUrl(link.Url)) link.Url = "#";

            var attributes = link.GetAttributes();
            attributes.AddPropertyIfNotExist("rel", "nofollow noopener noreferrer ugc");
            if (link.Url is { } url && url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                attributes.AddPropertyIfNotExist("target", "_blank");
        }

        // CommonMark autolinks (<scheme:...>) accept any scheme, including javascript:.
        foreach (var auto in document.Descendants<AutolinkInline>().ToList())
        {
            if (!IsSafeUrl(auto.Url)) auto.ReplaceBy(new LiteralInline(auto.Url));
        }
    }

    internal static bool IsSafeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        url = url.Trim();
        if (url.StartsWith('#')) return true;
        if (url.StartsWith('/')) return !url.StartsWith("//", StringComparison.Ordinal) && !url.StartsWith("/\\", StringComparison.Ordinal);
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" or "mailto";
    }
}

/// <summary>||hidden text|| renders as &lt;x-spoiler&gt;, which the stylesheet blurs until it is clicked.</summary>
internal sealed class SpoilerExtension : IMarkdownExtension
{
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
        var emphasis = pipeline.InlineParsers.Find<EmphasisInlineParser>();
        if (emphasis is not null && !emphasis.HasEmphasisChar('|'))
            emphasis.EmphasisDescriptors.Add(new EmphasisDescriptor('|', 2, 2, true));
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
        if (renderer is not HtmlRenderer html) return;
        var emphasisRenderer = html.ObjectRenderers.FindExact<EmphasisInlineRenderer>();
        if (emphasisRenderer is null) return;
        var fallback = emphasisRenderer.GetTag;
        emphasisRenderer.GetTag = inline => inline.DelimiterChar == '|' ? "x-spoiler" : fallback?.Invoke(inline);
    }
}
