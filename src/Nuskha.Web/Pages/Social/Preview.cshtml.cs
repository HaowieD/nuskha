namespace Nuskha.Web.Pages.Social;

/// <summary>Renders markup for the editor's Preview tab with exactly the renderer used for published text.</summary>
[Authorize]
[EnableRateLimiting("write")]
public class PreviewModel(IMarkdown markdown, ILoc t) : PageModel
{
    public IActionResult OnPost([FromForm] string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Content($"<p class=\"hint\">{System.Net.WebUtility.HtmlEncode(t["md.empty"])}</p>", "text/html");
        using var writer = new StringWriter();
        markdown.Render(text.Length > 20_000 ? text[..20_000] : text).WriteTo(writer, System.Text.Encodings.Web.HtmlEncoder.Default);
        return Content(writer.ToString(), "text/html");
    }
}
