using System.Globalization;
using System.Text.Json;

namespace Nuskha.Web.Services;

/// <summary>
/// Tiny JSON-based localizer (Localization/ka.json, en.json). Chosen over .resx so that translators can edit
/// plain JSON and keys stay greppable. Falls back to English, then to the key itself.
/// </summary>
public interface ILoc
{
    string this[string key] { get; }
    string Format(string key, params object[] args);
    bool IsEnglish { get; }
    string Lang { get; }
}

public sealed class JsonLoc : ILoc
{
    private readonly Dictionary<string, Dictionary<string, string>> _data = new();

    public JsonLoc(IWebHostEnvironment env)
    {
        foreach (var lang in new[] { "ka", "en" })
        {
            var path = Path.Combine(env.ContentRootPath, "Localization", lang + ".json");
            _data[lang] = File.Exists(path)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? new()
                : new();
        }
    }

    public string Lang => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en" ? "en" : "ka";
    public bool IsEnglish => Lang == "en";

    public string this[string key]
    {
        get
        {
            if (_data[Lang].TryGetValue(key, out var v)) return v;
            if (_data["en"].TryGetValue(key, out var e)) return e;
            return key;
        }
    }

    public string Format(string key, params object[] args) => string.Format(CultureInfo.CurrentCulture, this[key], args);
}
