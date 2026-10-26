namespace Nuskha.Web.Services.Email;

/// <summary>SMTP settings, bound from the "Email" configuration section. E-mail features switch on only when Host and From are set.</summary>
public sealed class EmailOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    /// <summary>True for implicit TLS (usually port 465); false uses STARTTLS when the server offers it (usually port 587).</summary>
    public bool ImplicitTls { get; set; }
    public string UserName { get; set; } = "";
    public string Password { get; set; } = "";
    /// <summary>Sender address, e.g. no-reply@nuskha.ge.</summary>
    public string From { get; set; } = "";
    public string FromName { get; set; } = "Nuskha";

    public bool Enabled => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(From);
}
