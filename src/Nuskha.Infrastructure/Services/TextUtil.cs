using System.Text;

namespace Nuskha.Infrastructure.Services;

public static class TextUtil
{
    // Georgian national romanization (2002) with apostrophes dropped so slugs stay URL-safe.
    private static readonly Dictionary<char, string> Ka = new()
    {
        ['ა'] = "a", ['ბ'] = "b", ['გ'] = "g", ['დ'] = "d", ['ე'] = "e", ['ვ'] = "v", ['ზ'] = "z",
        ['თ'] = "t", ['ი'] = "i", ['კ'] = "k", ['ლ'] = "l", ['მ'] = "m", ['ნ'] = "n", ['ო'] = "o",
        ['პ'] = "p", ['ჟ'] = "zh", ['რ'] = "r", ['ს'] = "s", ['ტ'] = "t", ['უ'] = "u", ['ფ'] = "p",
        ['ქ'] = "k", ['ღ'] = "gh", ['ყ'] = "q", ['შ'] = "sh", ['ჩ'] = "ch", ['ც'] = "ts", ['ძ'] = "dz",
        ['წ'] = "ts", ['ჭ'] = "ch", ['ხ'] = "kh", ['ჯ'] = "j", ['ჰ'] = "h",
    };

    /// <summary>Maps Georgian capitals (Mtavruli, U+1C90..U+1CBF) to their Mkhedruli letters.</summary>
    private static char FoldGeorgian(char c) =>
        c >= 'Ა' && c <= 'Ჿ' ? (char)(c - 0x1C90 + 0x10D0) : c;

    /// <summary>URL slug: lowercase ASCII, Georgian transliterated, words separated by '-'.</summary>
    public static string Slugify(string? text, int maxLength = 80)
    {
        if (string.IsNullOrWhiteSpace(text)) return "item";
        var sb = new StringBuilder();
        var lastDash = true;
        foreach (var raw in text.Normalize(NormalizationForm.FormKC))
        {
            var c = FoldGeorgian(raw);
            if (Ka.TryGetValue(c, out var latin)) { sb.Append(latin); lastDash = false; }
            else if (c < 128 && char.IsLetterOrDigit(c)) { sb.Append(char.ToLowerInvariant(c)); lastDash = false; }
            else if (!lastDash) { sb.Append('-'); lastDash = true; }
        }
        var s = sb.ToString().Trim('-');
        if (s.Length > maxLength) s = s[..maxLength].Trim('-');
        return s.Length == 0 ? "item" : s;
    }

    /// <summary>Comparison key: only letters/digits, lowercased, Georgian capitals folded.</summary>
    public static string Key(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var sb = new StringBuilder();
        foreach (var raw in text.Normalize(NormalizationForm.FormKC))
        {
            var c = FoldGeorgian(raw);
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    public static string BookKey(string title, string? firstAuthor) => Key(title) + "|" + Key(firstAuthor);

    public static string? NullIfBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

public static class IsbnHelper
{
    /// <summary>Returns a valid ISBN-13 (digits only) or null. ISBN-10 is converted.</summary>
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var chars = raw.Where(c => char.IsDigit(c) || c is 'x' or 'X').ToArray();
        var s = new string(chars).ToUpperInvariant();

        if (s.Length == 10 && Isbn10Valid(s))
        {
            var core = "978" + s[..9];
            return core + Isbn13CheckDigit(core);
        }
        if (s.Length == 13 && s.All(char.IsDigit) && Isbn13CheckDigit(s[..12]) == s[12])
            return s;
        return null;
    }

    private static bool Isbn10Valid(string s)
    {
        var sum = 0;
        for (var i = 0; i < 10; i++)
        {
            int v;
            if (s[i] == 'X') { if (i != 9) return false; v = 10; }
            else if (char.IsDigit(s[i])) v = s[i] - '0';
            else return false;
            sum += v * (10 - i);
        }
        return sum % 11 == 0;
    }

    private static char Isbn13CheckDigit(string first12)
    {
        var sum = 0;
        for (var i = 0; i < 12; i++) sum += (first12[i] - '0') * (i % 2 == 0 ? 1 : 3);
        return (char)('0' + (10 - sum % 10) % 10);
    }
}
