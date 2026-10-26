using System.Text.RegularExpressions;

namespace Nuskha.Infrastructure.Import;

/// <summary>Minimal robots.txt parser: Disallow/Allow/Crawl-delay for "*" and our own bot token.</summary>
public sealed class RobotsRules
{
    private readonly List<(bool Allow, Regex Pattern, int Length)> _rules = [];
    public int CrawlDelaySeconds { get; private set; }

    public static RobotsRules AllowAll() => new();

    public static RobotsRules Parse(string text, string botToken)
    {
        var result = new RobotsRules();
        var agents = new List<string>();
        var lastWasAgent = false;
        // Collect rules for the most specific matching group: our token beats "*".
        var groups = new List<(List<string> Agents, List<(bool Allow, string Path)> Rules, int Delay)>();
        List<(bool, string)> current = [];
        var delay = 0;

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Split('#')[0].Trim();
            if (line.Length == 0) continue;
            var idx = line.IndexOf(':');
            if (idx <= 0) continue;
            var field = line[..idx].Trim().ToLowerInvariant();
            var value = line[(idx + 1)..].Trim();

            if (field == "user-agent")
            {
                if (!lastWasAgent)
                {
                    current = [];
                    agents = [];
                    delay = 0;
                    groups.Add((agents, current, 0));
                }
                agents.Add(value.ToLowerInvariant());
                lastWasAgent = true;
                continue;
            }
            lastWasAgent = false;
            if (groups.Count == 0) continue;

            if (field is "disallow" or "allow")
                current.Add((field == "allow", value));
            else if (field == "crawl-delay" && int.TryParse(value, out var d))
            {
                delay = d;
                var last = groups[^1];
                groups[^1] = (last.Agents, last.Rules, delay);
            }
        }

        var token = botToken.ToLowerInvariant();
        var chosen = groups.FirstOrDefault(g => g.Agents.Any(a => a != "*" && token.Contains(a)));
        if (chosen.Agents is null) chosen = groups.FirstOrDefault(g => g.Agents.Contains("*"));
        if (chosen.Agents is null) return result;

        result.CrawlDelaySeconds = chosen.Delay;
        foreach (var (allow, path) in chosen.Rules)
        {
            if (path.Length == 0) continue; // empty Disallow = allow everything
            var pattern = "^" + Regex.Escape(path).Replace("\\*", ".*").Replace("\\$", "$");
            result._rules.Add((allow, new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant), path.Length));
        }
        return result;
    }

    /// <summary>Longest matching rule wins; Allow wins ties. No match = allowed.</summary>
    public bool IsAllowed(string pathAndQuery)
    {
        (bool Allow, int Length)? best = null;
        foreach (var (allow, pattern, length) in _rules)
        {
            if (!pattern.IsMatch(pathAndQuery)) continue;
            if (best is null || length > best.Value.Length || (length == best.Value.Length && allow))
                best = (allow, length);
        }
        return best?.Allow ?? true;
    }
}
