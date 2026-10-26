using System.Text.RegularExpressions;

namespace Nuskha.Web.Services;

public static partial class UsernameRules
{
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "administrator", "moderator", "mod", "editor", "nuskha", "system", "support", "staff", "root", "null", "me", "anonymous",
    };

    [GeneratedRegex("^[A-Za-z0-9_-]{3,24}$")]
    private static partial Regex Pattern();

    /// <summary>Returns a localization key describing the problem, or null when the username is acceptable.</summary>
    public static string? Validate(string? username)
    {
        if (string.IsNullOrWhiteSpace(username) || !Pattern().IsMatch(username)) return "val.username";
        if (Reserved.Contains(username)) return "val.username.reserved";
        return null;
    }

    /// <summary>Builds a URL-safe username suggestion from free text (e.g. an e-mail local part).</summary>
    public static string Suggest(string? text)
    {
        var cleaned = new string((text ?? "").Where(c => c < 128 && (char.IsLetterOrDigit(c) || c is '_' or '-')).ToArray());
        if (cleaned.Length < 3) cleaned = "reader" + cleaned;
        return cleaned.Length > 20 ? cleaned[..20] : cleaned;
    }
}
