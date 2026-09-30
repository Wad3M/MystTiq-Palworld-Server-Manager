// MystTiq v0.9.10.0: file reviewed for this release (2026-09-30).
using System.Text;
using System.Text.RegularExpressions;

namespace MystTiq.Desktop.Services;

/// <summary>
/// v0.9.1.0: translates the status and error messages the code builds (view-model text such as "Stopped / Not ready" or
/// "Restarted. 'Alpha' is now online.") on their way to the screen. The view models keep English, because logic and
/// tests compare it; only what is shown changes.
///
/// The msg.* keys in Assets/i18n map each English message to its translation. A message without placeholders is an
/// exact lookup (every key's English text counts, so a status that equals a label is translated too). A message with
/// {n} placeholders is a template: "Restarted. '{0}' is now online." matches the shown text, and the values it captures
/// are carried into the translated template, themselves translated when they are a known message, so
/// "Status: Stopped / Not ready" becomes "ステータス：停止 / 準備未完了". A message whose English ends in a space
/// ("The history could not be read: ") is the start of a longer text and matches anything that follows it. Text that
/// matches nothing is shown unchanged, as is everything while English is chosen.
///
/// v0.9.2.0: a text the code composes from several sentences (the map's summary, a Pal's tooltip) is translated sentence
/// by sentence, each sentence its own message. When every sentence is known that wins over a whole-text template, whose
/// first value would otherwise swallow the sentences before it.
///
/// v0.9.10.0: a value that is a name stays as it is. A server called "Ready" showed as "Bereit" in German because the value
/// of "Restarted. '{0}' is now online." was itself a known message. A placeholder in quotes ('{0}', "{0}", “{0}”) or right
/// after a word that names a thing (server, world, player, guild, profile, kit, mod, rule, channel, account, user, member)
/// holds a name, and its value is never translated. Other values still are ("Status: {0}").
/// </summary>
public sealed class MessageCatalog
{
    private static readonly Regex Placeholder = new(@"\{(\d+)(?::[^{}]*)?\}", RegexOptions.CultureInvariant);
    private const int MaxDepth = 2;
    private const int CacheLimit = 8192;

    private readonly Dictionary<string, string> exact;
    private readonly Template[] templates;
    private readonly Dictionary<string, string> cache = new(StringComparer.Ordinal);
    private readonly object gate = new();

    private MessageCatalog(Dictionary<string, string> exact, Template[] templates)
    {
        this.exact = exact;
        this.templates = templates;
    }

    public static MessageCatalog Empty { get; } = new(new Dictionary<string, string>(StringComparer.Ordinal), []);

    public int ExactCount => exact.Count;
    public int TemplateCount => templates.Length;

    /// <summary>Builds the catalog for one language from the English file and that language's file (msg.* and every other key).</summary>
    public static MessageCatalog Build(IReadOnlyDictionary<string, string> english, IReadOnlyDictionary<string, string> chosen)
    {
        if (ReferenceEquals(english, chosen)) return Empty;
        var exact = new Dictionary<string, string>(StringComparer.Ordinal);
        var templates = new List<Template>();
        foreach (var (key, en) in english)
        {
            if (string.IsNullOrWhiteSpace(en) || !chosen.TryGetValue(key, out var tr) || string.IsNullOrWhiteSpace(tr) || tr == en) continue;
            var isMessage = key.StartsWith("msg.", StringComparison.Ordinal);
            if (isMessage && Template.TryCreate(en, tr) is { } template)
            {
                templates.Add(template);
                continue;
            }

            // Labels (ui.*, ribbon.*) are exact matches only; the first key with a given English text wins.
            if (!Placeholder.IsMatch(en)) exact.TryAdd(en.Trim(), tr.Trim());
        }

        // The most specific template (the most fixed text) is tried first, so "Status: {0}" loses to "Status unavailable — {0}".
        // On a tie, a closed template beats one that is the start of a longer text ("Checked {0}." before "Checked ").
        return new MessageCatalog(exact, [.. templates.OrderByDescending(t => t.LiteralLength).ThenBy(t => t.IsOpen)]);
    }

    public string Translate(string? text)
    {
        if (string.IsNullOrEmpty(text) || (exact.Count == 0 && templates.Length == 0)) return text ?? string.Empty;
        lock (gate)
        {
            if (cache.TryGetValue(text, out var hit)) return hit;
        }

        var result = TranslateCore(text) ?? text;
        lock (gate)
        {
            if (cache.Count >= CacheLimit) cache.Clear();
            cache[text] = result;
        }

        return result;
    }

    // null when nothing matched, so a caller can tell "unchanged" from "translated to the same text".
    private string? TranslateCore(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0) return null;
        if (!trimmed.Contains('\n')) return TranslateLine(text);
        if (TranslateWhole(trimmed, 0) is { } whole) return Reedge(text, trimmed, whole);

        // A multi-line status (a heading and details) is translated line by line, each line whole or sentence by sentence.
        var lines = trimmed.Split('\n');
        var changed = false;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (TranslateLine(line) is { } translated)
            {
                lines[i] = lines[i].EndsWith('\r') ? translated + "\r" : translated;
                changed = true;
            }
        }

        return changed ? Reedge(text, trimmed, string.Join('\n', lines)) : null;
    }

    // One line: an exact message; else its sentences when every one is a message (so "{0} base(s) shown…" cannot
    // swallow the sentence before it as its value); else a template for the whole line; else whichever sentences match.
    private string? TranslateLine(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0) return null;
        if (exact.TryGetValue(trimmed, out var direct)) return Reedge(line, trimmed, direct);
        // v0.9.3.0: a template for the whole line wins when none of its values spans a sentence break: it is more
        // specific than the line's sentences matched one by one ("Ready: base {0} will transfer from {1} to {2}. Every
        // structure … updated to match." is one message, not "Ready: …" plus "Every {0}").
        if (TranslateWhole(trimmed, 0, unsplitValues: true) is { } whole) return Reedge(line, trimmed, whole);
        var (sentences, complete) = TranslateSentences(trimmed);
        var result = complete ? sentences : TranslateWhole(trimmed, 0) ?? sentences;
        return result is null ? null : Reedge(line, trimmed, result);
    }

    // v0.9.2.0: a text the code builds from several sentences ("3 player(s) online on the map. 2 base(s) shown from the
    // world save.") is translated sentence by sentence when it does not match whole; each sentence is its own message.
    private static readonly Regex SentenceBreak = new(@"(?<=[.!?…])(\s+)(?=[\p{Lu}\d""'(…])", RegexOptions.CultureInvariant);

    // (the text with each known sentence translated, or null if none is known; whether every sentence was known).
    private (string? Text, bool Complete) TranslateSentences(string text)
    {
        var parts = SentenceBreak.Split(text);
        if (parts.Length < 3) return (null, false);
        var changed = false;
        var complete = true;
        var result = new StringBuilder(text.Length);
        for (var i = 0; i < parts.Length; i++)
        {
            // Split keeps the captured whitespace: even entries are sentences, odd ones the spaces between them.
            if (i % 2 == 1) { result.Append(parts[i]); continue; }
            if (TranslateWhole(parts[i], 1) is { } sentence) { result.Append(sentence); changed = true; }
            else { result.Append(parts[i]); complete = false; }
        }

        return changed ? (result.ToString(), complete) : (null, false);
    }

    // An exact message or a template, the whole text; a template's values are translated too when each is a message.
    // unsplitValues: skip a match that would take a sentence break into a value (that is several messages, not one).
    private string? TranslateWhole(string trimmed, int depth, bool unsplitValues = false)
    {
        if (exact.TryGetValue(trimmed, out var direct)) return direct;
        foreach (var template in templates)
        {
            if (template.Match(trimmed) is not { } values) continue;
            if (unsplitValues && values.Any(v => SentenceBreak.IsMatch(v))) continue;
            if (depth < MaxDepth)
            {
                for (var i = 0; i < values.Length; i++)
                {
                    if (template.IsVerbatim(i)) continue;
                    if (values[i] is { Length: > 0 } value && value.Trim() is { Length: > 0 } v && TranslateWhole(v, depth + 1) is { } inner) values[i] = Reedge(value, v, inner);
                }
            }

            return template.Fill(values);
        }

        return null;
    }

    // Keeps the shown text's own leading/trailing spaces around the translation.
    private static string Reedge(string original, string trimmed, string translated)
    {
        if (original.Length == trimmed.Length) return translated;
        var lead = original[..(original.Length - original.TrimStart().Length)];
        var trail = original[original.TrimEnd().Length..];
        return lead + translated + trail;
    }

    private sealed class Template
    {
        private readonly Regex regex;
        private readonly string prefix;
        private readonly string suffix;
        private readonly int count;
        private readonly string translation;
        private readonly HashSet<int> verbatim;

        private Template(Regex regex, string prefix, string suffix, int count, string translation, int literalLength, bool isOpen, HashSet<int> verbatim)
        {
            IsOpen = isOpen;
            this.verbatim = verbatim;
            this.regex = regex;
            this.prefix = prefix;
            this.suffix = suffix;
            this.count = count;
            this.translation = translation;
            LiteralLength = literalLength;
        }

        public int LiteralLength { get; }
        public bool IsOpen { get; }

        // v0.9.10.0: whether placeholder n holds a name (see the class summary).
        public bool IsVerbatim(int n) => verbatim.Contains(n);

        private const string OpeningQuotes = "'\"“‘«„「『";
        private const string ClosingQuotes = "'\"”’»“」』";
        private static readonly Regex NameNoun = new(@"(?i)\b(server|world|player|guild|profile|tab|kit|mod|rule|channel|account|user|member|named)\s$",
            RegexOptions.CultureInvariant);

        private static bool HoldsName(string english, Match placeholder)
        {
            var before = placeholder.Index > 0 ? english[placeholder.Index - 1] : '\0';
            var afterIndex = placeholder.Index + placeholder.Length;
            var after = afterIndex < english.Length ? english[afterIndex] : '\0';
            if (OpeningQuotes.Contains(before) && ClosingQuotes.Contains(after)) return true;
            return NameNoun.IsMatch(english[..placeholder.Index]);
        }

        public static Template? TryCreate(string english, string translated)
        {
            var en = english.Trim();
            var tr = translated.Trim();
            var open = english.Length > english.TrimEnd().Length;
            var matches = Placeholder.Matches(en);
            if (matches.Count == 0 && !open) return null;

            var pattern = new StringBuilder("^");
            var seen = new HashSet<int>();
            var names = new HashSet<int>();
            var last = 0;
            var literal = 0;
            var letters = 0;
            var max = -1;
            foreach (Match m in matches)
            {
                var piece = en[last..m.Index];
                literal += piece.Count(c => !char.IsWhiteSpace(c));
                letters += piece.Count(char.IsLetter);
                pattern.Append(Regex.Escape(piece));
                var n = int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                max = Math.Max(max, n);
                if (HoldsName(en, m)) names.Add(n);
                pattern.Append(seen.Add(n) ? $"(?<p{n}>.*?)" : $@"\k<p{n}>");
                last = m.Index + m.Length;
            }

            var tail = en[last..];
            literal += tail.Count(c => !char.IsWhiteSpace(c));
            letters += tail.Count(char.IsLetter);
            pattern.Append(Regex.Escape(tail));

            // "The history could not be read: " is followed by the error; that rest becomes one more value.
            if (open)
            {
                max++;
                pattern.Append($@"\s*(?<p{max}>.+)");
                tr = translated.TrimEnd() + (translated.Length > translated.TrimEnd().Length ? translated[translated.TrimEnd().Length..] : " ") + "{" + max + "}";
            }

            pattern.Append('$');

            // Too little fixed text would match unrelated values: at least four fixed characters, two of them letters
            // ("Day {0} • {1}" and "Steam {0}" count; "{0}{1}", "{0} MB" and "Up {0}" do not).
            if (literal < 4 || letters < 2) return null;

            var firstPlaceholder = matches.Count > 0 ? matches[0].Index : en.Length;
            var prefix = en[..firstPlaceholder];
            var suffix = open || matches.Count == 0 ? string.Empty : en[(matches[^1].Index + matches[^1].Length)..];
            return new Template(
                new Regex(pattern.ToString(), RegexOptions.Singleline | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50)),
                prefix, suffix, max + 1, tr, literal, open, names);
        }

        public string[]? Match(string text)
        {
            if (!text.StartsWith(prefix, StringComparison.Ordinal) || !text.EndsWith(suffix, StringComparison.Ordinal)) return null;
            if (text.Length < prefix.Length + suffix.Length) return null;
            Match m;
            try { m = regex.Match(text); }
            catch (RegexMatchTimeoutException) { return null; }
            if (!m.Success) return null;
            var values = new string[count];
            for (var i = 0; i < count; i++) values[i] = m.Groups[$"p{i}"] is { Success: true } g ? g.Value : string.Empty;
            return values;
        }

        public string Fill(string[] values) => Placeholder.Replace(translation, m =>
        {
            var n = int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            return n < values.Length ? values[n] : m.Value;
        });
    }
}
