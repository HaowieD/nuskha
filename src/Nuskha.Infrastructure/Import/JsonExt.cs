using System.Text.Json;

namespace Nuskha.Infrastructure.Import;

internal static class JsonExt
{
    public static string? Str(this JsonElement e, string name)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(name, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.GetRawText(),
            _ => null,
        };
    }

    public static int? Int(this JsonElement e, string name)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(name, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)) return i;
        if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var j)) return j;
        return null;
    }

    public static List<string> StrList(this JsonElement e, string name)
    {
        var list = new List<string>();
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(name, out var v)) return list;
        if (v.ValueKind == JsonValueKind.Array)
        {
            foreach (var x in v.EnumerateArray())
                if (x.ValueKind == JsonValueKind.String && x.GetString() is { Length: > 0 } s) list.Add(s);
        }
        else if (v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } one) list.Add(one);
        return list;
    }

    /// <summary>First 4-digit year found in text like "2019-05-01" or "May 2019".</summary>
    public static int? YearFrom(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        for (var i = 0; i + 4 <= text.Length; i++)
        {
            if (char.IsDigit(text[i]) && char.IsDigit(text[i + 1]) && char.IsDigit(text[i + 2]) && char.IsDigit(text[i + 3])
                && int.TryParse(text.AsSpan(i, 4), out var y) && y is >= 1400 and <= 2100)
                return y;
        }
        return null;
    }
}
