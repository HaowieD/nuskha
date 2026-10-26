using Nuskha.Web.Services;

namespace Nuskha.Tests;

public class UsernameRulesTests
{
    [Theory]
    [InlineData("haowie")]
    [InlineData("reader_1")]
    [InlineData("Book-Worm")]
    public void Accepts_valid_usernames(string name) => Assert.Null(UsernameRules.Validate(name));

    [Theory]
    [InlineData("ab")]
    [InlineData("has space")]
    [InlineData("გიორგი")]
    [InlineData("this_name_is_way_too_long_for_us")]
    [InlineData("")]
    public void Rejects_malformed_usernames(string name) => Assert.Equal("val.username", UsernameRules.Validate(name));

    [Theory]
    [InlineData("admin")]
    [InlineData("Moderator")]
    public void Rejects_reserved_usernames(string name) => Assert.Equal("val.username.reserved", UsernameRules.Validate(name));

    [Fact]
    public void Suggest_strips_unsafe_characters() => Assert.Equal("johndoe", UsernameRules.Suggest("john.doe"));

    [Fact]
    public void Suggest_pads_short_input() => Assert.Equal("readerab", UsernameRules.Suggest("ab"));

    [Fact]
    public void Suggest_truncates_long_input() => Assert.Equal(20, UsernameRules.Suggest(new string('a', 40)).Length);
}
