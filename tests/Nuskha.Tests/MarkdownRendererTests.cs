using Nuskha.Web.Services;

namespace Nuskha.Tests;

public class MarkdownRendererTests
{
    private static string Html(string? text)
    {
        using var w = new StringWriter();
        new MarkdownRenderer().Render(text).WriteTo(w, System.Text.Encodings.Web.HtmlEncoder.Default);
        return w.ToString();
    }

    [Fact]
    public void Raw_html_is_escaped() =>
        Assert.DoesNotContain("<script", Html("<script>alert(1)</script> hi <img src=x onerror=alert(1)>"), StringComparison.OrdinalIgnoreCase);

    [Theory]
    [InlineData("[x](javascript:alert(1))")]
    [InlineData("[x](JaVaScRiPt:alert(1))")]
    [InlineData("[x](data:text/html;base64,AAAA)")]
    [InlineData("<javascript:alert(1)>")]
    [InlineData("[x](//evil.example/x)")]
    public void Dangerous_links_never_reach_href(string input)
    {
        var html = Html(input);
        // Plain text that merely reads "javascript:..." is harmless; what matters is that no href can carry it.
        Assert.DoesNotMatch(new System.Text.RegularExpressions.Regex("href\\s*=\\s*[\"']?\\s*javascript", System.Text.RegularExpressions.RegexOptions.IgnoreCase), html);
        Assert.DoesNotContain("href=\"data:", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=\"//", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Images_are_not_rendered() => Assert.DoesNotContain("<img", Html("![alt](https://example.com/a.png)"), StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void External_links_are_nofollow_and_open_in_new_tab()
    {
        var html = Html("[site](https://example.com)");
        Assert.Contains("href=\"https://example.com\"", html);
        Assert.Contains("nofollow", html);
        Assert.Contains("target=\"_blank\"", html);
    }

    [Fact]
    public void Basic_formatting_and_spoilers_render()
    {
        var html = Html("**bold** *it* ~~gone~~ ||secret||");
        Assert.Contains("<strong>bold</strong>", html);
        Assert.Contains("<em>it</em>", html);
        Assert.Contains("<del>gone</del>", html);
        Assert.Contains("<x-spoiler>secret</x-spoiler>", html);
    }

    [Fact]
    public void Empty_input_renders_nothing() => Assert.Equal("", Html("   "));
}
