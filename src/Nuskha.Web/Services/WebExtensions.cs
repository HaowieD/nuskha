using System.Security.Claims;
using Nuskha.Domain;
using Nuskha.Domain.Entities;

namespace Nuskha.Web.Services;

public static class Policies
{
    public const string Staff = "Staff";                 // Admin, Moderator, Editor
    public const string ManageCatalog = "ManageCatalog"; // Admin, Editor
    public const string Moderate = "Moderate";           // Admin, Moderator
    public const string AdminOnly = "AdminOnly";         // Admin
}

public static class WebExtensions
{
    public static Guid? GetUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public static bool IsStaff(this ClaimsPrincipal user) =>
        user.IsInRole(Roles.Admin) || user.IsInRole(Roles.Moderator) || user.IsInRole(Roles.Editor);

    public static bool CanModerate(this ClaimsPrincipal user) =>
        user.IsInRole(Roles.Admin) || user.IsInRole(Roles.Moderator);

    public static bool CanManageCatalog(this ClaimsPrincipal user) =>
        user.IsInRole(Roles.Admin) || user.IsInRole(Roles.Editor);

    public static string DisplayTitle(this Book b, bool english) =>
        english && !string.IsNullOrWhiteSpace(b.TitleEn) ? b.TitleEn! : b.Title;

    public static string Pick(this ILoc t, string ka, string? en) =>
        t.IsEnglish && !string.IsNullOrWhiteSpace(en) ? en! : ka;

    /// <summary>Relative time ("5 min ago") for recent dates, otherwise dd.MM.yyyy.</summary>
    public static string TimeAgo(this ILoc t, DateTime utc)
    {
        var d = DateTime.UtcNow - utc;
        if (d.TotalMinutes < 1) return t["time.now"];
        if (d.TotalHours < 1) return t.Format("time.minutes", (int)d.TotalMinutes);
        if (d.TotalDays < 1) return t.Format("time.hours", (int)d.TotalHours);
        if (d.TotalDays < 7) return t.Format("time.days", (int)d.TotalDays);
        return utc.ToString("dd.MM.yyyy");
    }

    /// <summary>Stable 0..7 bucket so books without a cover get a consistent colored tile.</summary>
    public static int CoverBucket(string? slug)
    {
        var h = 17;
        foreach (var c in slug ?? "") h = unchecked(h * 31 + c);
        return Math.Abs(h % 8);
    }

    public static bool IsLocalUrl(string? url) =>
        !string.IsNullOrEmpty(url) && url[0] == '/' && (url.Length == 1 || (url[1] != '/' && url[1] != '\\'));

    /// <summary>Escapes LIKE wildcards so user input is matched literally.</summary>
    public static string EscapeLike(string s) =>
        s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
