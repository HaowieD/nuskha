using Nuskha.Web.Services.Email;

namespace Nuskha.Tests;

public class AccountEmailsTests
{
    [Theory]
    [InlineData("CfDJ8N+/abc==")]
    [InlineData("a b&c=d?e")]
    public void Token_round_trips_through_url_safe_encoding(string token)
    {
        var encoded = AccountEmails.EncodeToken(token);
        Assert.DoesNotContain('+', encoded);
        Assert.DoesNotContain('/', encoded);
        Assert.DoesNotContain('=', encoded);
        Assert.Equal(token, AccountEmails.DecodeToken(encoded));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("!!!not base64!!!")]
    public void Bad_codes_decode_to_null(string? code) => Assert.Null(AccountEmails.DecodeToken(code));

    [Fact]
    public void Options_are_disabled_until_host_and_sender_are_set()
    {
        Assert.False(new EmailOptions().Enabled);
        Assert.False(new EmailOptions { Host = "smtp.example.com" }.Enabled);
        Assert.True(new EmailOptions { Host = "smtp.example.com", From = "no-reply@example.com" }.Enabled);
    }
}
