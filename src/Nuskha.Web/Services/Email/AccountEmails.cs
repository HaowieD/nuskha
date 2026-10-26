using System.Net;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace Nuskha.Web.Services.Email;

/// <summary>
/// Builds and sends the account mails (confirm address, reset password) in the visitor's current language.
/// Links are built from Site:BaseUrl rather than the Host header, so a forged Host header cannot make us mail
/// out a reset link that points at an attacker's domain.
/// </summary>
public sealed class AccountEmails(IAppEmailSender sender, ILoc t, IConfiguration config, ILogger<AccountEmails> log)
{
    public bool Enabled => sender.Enabled;

    public static string EncodeToken(string token) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    public static string? DecodeToken(string? code)
    {
        if (string.IsNullOrEmpty(code)) return null;
        try { return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code)); }
        catch (FormatException) { return null; }
    }

    public Task<bool> SendConfirmationAsync(AppUser user, string token, CancellationToken ct = default) =>
        SendAsync(user, "mail.confirm.subject", "mail.confirm.body",
            $"/account/confirm-email?userId={user.Id}&code={EncodeToken(token)}", ct);

    public Task<bool> SendPasswordResetAsync(AppUser user, string token, CancellationToken ct = default) =>
        SendAsync(user, "mail.reset.subject", "mail.reset.body",
            $"/account/reset-password?userId={user.Id}&code={EncodeToken(token)}", ct);

    /// <summary>Returns false (and logs) instead of throwing, so callers can show the same neutral message either way.</summary>
    private async Task<bool> SendAsync(AppUser user, string subjectKey, string bodyKey, string path, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(user.Email)) return false;
        var link = (config["Site:BaseUrl"]?.TrimEnd('/') ?? "") + path;
        var name = string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName ?? "" : user.DisplayName;

        var intro = t.Format(bodyKey, name);
        var text = $"{intro}\n\n{link}\n\n{t["mail.expires"]}\n{t["mail.ignore"]}\n";
        var html = $"<p>{WebUtility.HtmlEncode(intro)}</p>" +
                   $"<p><a href=\"{WebUtility.HtmlEncode(link)}\">{WebUtility.HtmlEncode(link)}</a></p>" +
                   $"<p>{WebUtility.HtmlEncode(t["mail.expires"])}<br>{WebUtility.HtmlEncode(t["mail.ignore"])}</p>";
        try
        {
            await sender.SendAsync(user.Email, t[subjectKey], text, html, ct);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            log.LogError(ex, "Could not send {Kind} e-mail to user {UserId}", subjectKey, user.Id);
            return false;
        }
    }
}
