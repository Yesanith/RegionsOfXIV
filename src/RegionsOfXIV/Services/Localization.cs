using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace RegionsOfXIV.Services;

// The plugin's own interface strings. Place, weather and banner names are not here -- those come
// out of game data and are already in the player's language.
//
// English is an argument at every call site rather than an entry in a file. That is what makes the
// fallback total: a missing key, a blank translation, a file that will not parse, a language never
// shipped -- all of them end at English that is compiled in. Nothing can put a raw key on screen,
// and a half-finished translation reads as partly English rather than as a broken window.
//
// en.json exists for translators to work from and is never read at runtime, so it cannot disagree
// with what is drawn.
internal static class Loc
{
    private const string ResourcePrefix = "RegionsOfXIV.Localization.";

    private const string ResourceSuffix = ".json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static string[]? shipped;

    private static Dictionary<string, string> active = [];

    // Label and Unit build a string every time they are called, and every labelled widget on the
    // visible tab calls one of them every frame. These hold the built result per key.
    //
    // Only those two are cached, and only because every one of their call sites is in UI/ and so
    // runs on the draw thread. Get and Format are reached from the framework thread and from the
    // sound decode thread as well, and a dictionary written from three threads is a fault that
    // shows up once a month and never in a way anyone can reproduce.
    private static readonly Dictionary<string, string> labels = [];

    private static readonly Dictionary<string, string> units = [];

    // Bumped whenever the table is swapped. The caches are cleared against it by whoever reads
    // them next rather than by Apply, for the same reason: Apply is on the framework thread and
    // clearing a dictionary out from under the thread enumerating it is the hazard being avoided.
    private static int generation;

    private static int cachedGeneration = -1;

    // Numbers are formatted in the language of the sentence around them, not in whatever the
    // operating system happens to be set to. A German window reading "50.5 %" while the rest of
    // Windows says "50,5 %" looks like a bug, and the OS culture is what CurrentCulture would have
    // given us -- the two are unrelated settings.
    private static CultureInfo culture = CultureInfo.InvariantCulture;

    // Read off the bundled resources rather than listed here, so a fifth language really is a file
    // and a build-action line. English is not among them: it is the fallback, so a file for it
    // would be a second copy of every string with nothing to keep the two in step.
    public static IReadOnlyList<string> Shipped => shipped ??= Discover();

    public static string Current { get; private set; } = "en";

    // Whether the loaded file marks itself a machine draft. The notice in the config window hangs
    // off this rather than off "the language is not English", so a translation that someone has
    // been through stops apologising for itself the moment the marker comes out of the file.
    public static bool IsMachineDraft { get; private set; }

    public static string Get(string key, string english) =>
        active.TryGetValue(key, out var translated) && !string.IsNullOrWhiteSpace(translated)
            ? translated
            : english;

    // For anything ImGui gives an identity to -- every labelled widget, every tab. ImGui hashes the
    // label to get that identity, so a translated label is a different widget in every language:
    // the tab you were on resets when the language changes, a slider being dragged detaches, and
    // two labels that happen to translate alike become one control that will not respond.
    //
    // Everything after "###" is the identity and none of it is drawn, so the key does that job. It
    // is already unique and, unlike the label, it never changes. Tooltips have no identity and want
    // Get instead.
    public static string Label(string key, string english)
    {
        DropStaleCaches();

        if (labels.TryGetValue(key, out var cached))
            return cached;

        return labels[key] = Get(key, english) + "###" + key;
    }

    // For a unit appended to a printf specifier -- the "px" in "%.1f px". That whole string is
    // handed to ImGui and on to sprintf, which reads a per-cent sign as the opening of a
    // specifier, so a translation containing one would arrive as a second specifier with no
    // argument behind it. Doubling gives the literal per-cent sign the translator meant.
    //
    // Escaped here rather than checked when the file loads: escaping holds for anything a file
    // can contain, where a check only refuses what it was written to look for.
    public static string Unit(string key, string english)
    {
        DropStaleCaches();

        if (units.TryGetValue(key, out var cached))
            return cached;

        return units[key] = Get(key, english).Replace("%", "%%");
    }

    // Both caches go together: they are invalidated by the same event and a key is never in one
    // and not the other for a reason worth telling apart.
    private static void DropStaleCaches()
    {
        if (cachedGeneration == generation)
            return;

        labels.Clear();
        units.Clear();
        cachedGeneration = generation;
    }

    // Placeholders are the one thing a translator writes that the runtime has to execute, so a
    // mangled "{0}" must not reach the draw as an exception. The English pattern is tried next,
    // and if that one is malformed too it is shown unformatted rather than thrown.
    public static string Format(string key, string english, params object?[] args)
    {
        var pattern = Get(key, english);

        try
        {
            return string.Format(culture, pattern, args);
        }
        catch (FormatException)
        {
            Log.Warning($"Placeholders in \"{key}\" are malformed for {Current}; using English.");
        }

        try
        {
            return string.Format(culture, english, args);
        }
        catch (FormatException)
        {
            return english;
        }
    }

    // Anything not shipped, unreadable or empty leaves the interface in English.
    public static void Use(string? languageCode)
    {
        if (Match(languageCode) is not { } code)
        {
            Apply("en", []);
            return;
        }

        var loaded = Read(code, out var status);

        if (loaded is null)
        {
            Apply("en", []);
            return;
        }

        Apply(code, loaded, status);
    }

    // Swapped in one go, after the new table has been built. A file that will not parse never gets
    // this far, so a bad language leaves what is on screen alone instead of emptying the window
    // part-way through the change.
    internal static void Apply(string code, Dictionary<string, string> strings, string? status = null)
    {
        if (strings.Count > 0)
            WarnAboutGlyphsTheWindowLacks(code, strings);

        active = strings;
        Current = strings.Count > 0 ? code : "en";
        culture = CultureFor(Current);
        IsMachineDraft = strings.Count > 0
                         && status?.Contains("machine-drafted", StringComparison.OrdinalIgnoreCase) == true;

        // Last, so a reader that has already passed DropStaleCaches on the old generation cannot
        // fill a cache from the new table and stamp it with the old number.
        generation++;
    }

    // Invariant when the runtime has no data for the code, so a language file with a plausible but
    // unusable name cannot take the window down.
    private static CultureInfo CultureFor(string code)
    {
        try
        {
            return CultureInfo.GetCultureInfo(code);
        }
        catch (CultureNotFoundException)
        {
            Log.Warning($"No culture data for \"{code}\"; numbers will be formatted invariantly.");
            return CultureInfo.InvariantCulture;
        }
    }

    // Dalamud hands out codes like "en", "de" or "zh". Anything not shipped falls to English
    // rather than to the nearest guess.
    private static string? Match(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
            return null;

        var wanted = languageCode.Trim();

        // Exact first, so a pt-BR file wins over a pt one when both are shipped.
        foreach (var code in Shipped)
        {
            if (string.Equals(code, wanted, StringComparison.OrdinalIgnoreCase))
                return code;
        }

        // Then the primary subtag, so a pt-BR file still answers Dalamud asking for "pt".
        var primary = wanted.Split('-')[0];

        foreach (var code in Shipped)
        {
            if (code.Split('-')[0].Equals(primary, StringComparison.OrdinalIgnoreCase))
                return code;
        }

        return null;
    }

    private static string[] Discover()
    {
        try
        {
            return typeof(Loc).Assembly.GetManifestResourceNames()
                .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal)
                            && n.EndsWith(ResourceSuffix, StringComparison.Ordinal))
                .Select(n => n[ResourcePrefix.Length..^ResourceSuffix.Length])
                .Where(LooksLikeLanguageCode)
                .OrderBy(c => c, StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not list the bundled languages; the interface stays in English.");
            return [];
        }
    }

    // "de", "pt-BR", "zh-Hans". Shape only -- whether the code means anything is the runtime's
    // business, not ours. Tight enough that a stray file dropped in the folder is not offered to
    // players as a language.
    internal static bool LooksLikeLanguageCode(string code)
    {
        var parts = code.Split('-');

        if (parts.Length > 2)
            return false;

        if (parts[0].Length is < 2 or > 3 || !parts[0].All(char.IsAsciiLetter))
            return false;

        return parts.Length == 1
               || (parts[1].Length is >= 2 and <= 4 && parts[1].All(char.IsAsciiLetterOrDigit));
    }

    private static Dictionary<string, string>? Read(string code, out string? status)
    {
        status = null;

        try
        {
            using var stream = typeof(Loc).Assembly
                .GetManifestResourceStream(ResourcePrefix + code + ResourceSuffix);

            if (stream is null)
            {
                Log.Warning($"No {code} strings are bundled; the interface stays in English.");
                return null;
            }

            return Parse(stream, out status);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Could not read the {code} strings; the interface stays in English.");
            return null;
        }
    }

    // Anything it does not recognise is skipped rather than rejected, so one malformed entry costs
    // that one string and not the whole language.
    internal static Dictionary<string, string> Parse(Stream json) => Parse(json, out _);

    internal static Dictionary<string, string> Parse(Stream json, out string? status)
    {
        var raw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions);

        var strings = new Dictionary<string, string>(raw?.Count ?? 0);
        status = null;

        if (raw is null)
            return strings;

        foreach (var (key, value) in raw)
        {
            // Keys opening with an underscore are notes to whoever edits the file rather than
            // strings to show. "_status" is the one the plugin itself reads, to know whether the
            // file is still a machine draft.
            if (key.StartsWith('_'))
            {
                if (key == "_status" && value.ValueKind == JsonValueKind.String)
                    status = value.GetString();

                continue;
            }

            if (value.ValueKind != JsonValueKind.Object)
                continue;

            if (TryMessage(value, out var message))
                strings[key] = message;
        }

        return strings;
    }

    private static bool TryMessage(JsonElement entry, out string message)
    {
        message = string.Empty;

        foreach (var property in entry.EnumerateObject())
        {
            if (!property.NameEquals("message") || property.Value.ValueKind != JsonValueKind.String)
                continue;

            var text = property.Value.GetString();

            if (string.IsNullOrWhiteSpace(text))
                return false;

            message = text;
            return true;
        }

        return false;
    }

    // The window no longer draws with the game's AXIS face alone: UI/WindowFont merges the Windows
    // interface font in behind it for Latin Extended-A, Latin Extended-B and Latin Extended
    // Additional. Turkish, Polish, Czech, Romanian and Vietnamese are drawable because of that
    // merge, and were not before it.
    //
    // What is still missing is what neither face supplies: Cyrillic beyond the Russian alphabet
    // AXIS carries, and the scripts the merge deliberately leaves out because they are large and
    // nothing asks for them.
    //
    // Glyph ranges are fixed when the atlas is built, so nothing recovers them at draw time, and
    // widening the merge is a real change rather than a file drop. Hence saying so at load rather
    // than leaving whoever added the file to work out why half of it is missing.
    private static void WarnAboutGlyphsTheWindowLacks(string code, Dictionary<string, string> strings)
    {
        var missing = strings.Values
            .SelectMany(text => text)
            .Where(WindowFontLacks)
            .Distinct()
            .Take(12)
            .ToArray();

        if (missing.Length == 0)
            return;

        Log.Warning(
            $"The {code} strings use characters the game's AXIS font has no glyph for, so they "
            + $"will draw as blanks: {string.Join(" ", missing.Select(c => $"{c} (U+{(int)c:X4})"))}. "
            + "Showing them would mean giving the config window its own font.");
    }

    // What the window still cannot draw once the merge in UI/WindowFont is counted. Deliberately a
    // list of what is absent rather than of what is present: a rare kanji outside AXIS's ~6300
    // slips through, but nothing legitimate gets flagged, and a warning that cries wolf is one
    // nobody reads.
    //
    // The extended Latin blocks are gone from this list because the merge supplies them. Cyrillic
    // stays: the merge covers Latin only, so AXIS's Russian alphabet is still the whole of it.
    internal static bool WindowFontLacks(char c) =>
        (c is >= 'Ѐ' and <= 'ԯ' // Cyrillic, except the Russian alphabet below
         && c is not ('Ё' or 'ё') // Yo, yo
         && c is not (>= 'А' and <= 'я')) // A to ya
        || c is >= '֐' and <= 'ۿ' // Hebrew and Arabic
        || c is >= '฀' and <= '๿' // Thai
        || c is >= '가' and <= '힯'; // Hangul syllables
}
