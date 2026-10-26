using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Nuskha.Web.Services.Email;

public interface IAppEmailSender
{
    bool Enabled { get; }
    Task SendAsync(string to, string subject, string textBody, string htmlBody, CancellationToken ct = default);
}

/// <summary>
/// Sends mail through any SMTP server (MailKit). A fresh connection per message is fine at this volume
/// (confirmation and reset mails only) and avoids holding a socket open that the server may drop.
/// </summary>
public sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IAppEmailSender
{
    private readonly EmailOptions _o = options.Value;

    public bool Enabled => _o.Enabled;

    public async Task SendAsync(string to, string subject, string textBody, string htmlBody, CancellationToken ct = default)
    {
        if (!Enabled) throw new InvalidOperationException("E-mail is not configured.");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_o.FromName, _o.From));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new BodyBuilder { TextBody = textBody, HtmlBody = htmlBody }.ToMessageBody();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));

        using var client = new SmtpClient { Timeout = 20_000 };
        var security = _o.ImplicitTls ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        await client.ConnectAsync(_o.Host, _o.Port, security, timeout.Token);
        if (!string.IsNullOrEmpty(_o.UserName))
            await client.AuthenticateAsync(_o.UserName, _o.Password, timeout.Token);
        await client.SendAsync(message, timeout.Token);
        await client.DisconnectAsync(true, timeout.Token);
    }
}
