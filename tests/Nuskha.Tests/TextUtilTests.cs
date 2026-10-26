using Nuskha.Infrastructure.Services;

namespace Nuskha.Tests;

public class TextUtilTests
{
    [Theory]
    [InlineData("თბილისი", "tbilisi")]
    [InlineData("ვეფხისტყაოსანი", "vepkhistqaosani")]
    [InlineData("Hello,  World!", "hello-world")]
    [InlineData("  --  ", "item")]
    [InlineData("", "item")]
    public void Slugify_transliterates_and_normalizes(string input, string expected) =>
        Assert.Equal(expected, TextUtil.Slugify(input));

    [Fact]
    public void Slugify_folds_Georgian_capitals() =>
        Assert.Equal("tbilisi", TextUtil.Slugify("ᲗᲑᲘᲚᲘᲡᲘ"));

    [Fact]
    public void Slugify_null_is_item() => Assert.Equal("item", TextUtil.Slugify(null));

    [Fact]
    public void Slugify_respects_max_length_without_trailing_dash() =>
        Assert.Equal("aaaa", TextUtil.Slugify("aaaa bbbb", 5));

    [Fact]
    public void Key_keeps_only_letters_and_digits() =>
        Assert.Equal("helloworld1", TextUtil.Key("Hello, World! 1"));

    [Fact]
    public void BookKey_combines_title_and_first_author() =>
        Assert.Equal("ab|cd", TextUtil.BookKey("A b", "C d"));

    [Theory]
    [InlineData("978-0-306-40615-7", "9780306406157")]
    [InlineData("0-306-40615-2", "9780306406157")]
    [InlineData("080442957X", "9780804429573")]
    [InlineData("9780306406158", null)]
    [InlineData("not an isbn", null)]
    [InlineData(null, null)]
    public void Isbn_normalizes_and_validates(string? input, string? expected) =>
        Assert.Equal(expected, IsbnHelper.Normalize(input));
}
