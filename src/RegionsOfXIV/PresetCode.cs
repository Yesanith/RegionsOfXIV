using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RegionsOfXIV.Services;

namespace RegionsOfXIV;

// A whole preset squeezed into one line of text that survives being pasted into game chat.
//
// Only settings that differ from the defaults are stored, which keeps a typical code short
// enough to paste and means a code made by an older build still applies cleanly -- anything it
// does not mention simply stays at whatever this build defaults to.
internal static class PresetCode
{
    private const string Prefix = "ROX1-";

    private const int MaxCodeLength = 8 * 1024;
    private const int MaxDecompressedBytes = 64 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        IncludeFields = true,
        Converters = { new TicksTimeSpanConverter() },
    };

    public static string Encode(string name, Configuration settings)
    {
        var payload = new Dictionary<string, object?>
        {
            ["n"] = name,
            ["v"] = Configuration.CurrentVersion,
            ["s"] = Overrides(settings),
        };

        var json = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);

        using var compressed = new MemoryStream();

        using (var deflate = new DeflateStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
            deflate.Write(json, 0, json.Length);

        return Prefix + ToBase64Url(compressed.ToArray());
    }

    public static bool TryDecode(string code, out UserPreset? preset, out string error)
    {
        preset = null;

        var trimmed = Clean(code);

        if (trimmed.Length == 0)
        {
            error = Loc.Get(
                "presetcode.empty", "There is nothing on the clipboard to import.");
            return false;
        }

        if (trimmed.Length > MaxCodeLength)
        {
            error = Loc.Get(
                "presetcode.toolong", "That code is too long to be one of ours.");
            return false;
        }

        if (!trimmed.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            error = Loc.Format(
                "presetcode.notacode",
                "That does not look like a preset code — they start with \"{0}\".",
                Prefix);
            return false;
        }

        try
        {
            var json = Inflate(FromBase64Url(trimmed[Prefix.Length..]));

            if (json == null)
            {
                error = Loc.Get(
                    "presetcode.unreadable", "That code did not unpack to anything readable.");
                return false;
            }

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                error = Loc.Get(
                    "presetcode.notapreset", "That code did not unpack to a preset.");
                return false;
            }

            var name = root.TryGetProperty("n", out var nameElement) && nameElement.ValueKind == JsonValueKind.String
                ? nameElement.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(name))
            {
                error = Loc.Get(
                    "presetcode.noname", "That code carries no preset name.");
                return false;
            }

            var built = new UserPreset { Name = name.Trim() };

            if (root.TryGetProperty("s", out var overrides) && overrides.ValueKind == JsonValueKind.Object)
                ApplyOverrides(overrides, built.Settings);

            // A code carries the config version it was written against, and settings have been
            // replaced since -- 0.3.0.0 wrote OverlapHeader where this build reads HeaderGap.
            // Running the same migration the config file gets is what stops an old code quietly
            // landing on the default for everything that has been superseded since.
            built.Settings.Version = VersionOf(root);
            built.Settings.Migrate();

            preset = built;
            error = string.Empty;
            return true;
        }
        catch (Exception)
        {
            error = Loc.Get(
                "presetcode.damaged",
                "That code is damaged — some of it went missing on the way here. "
                + "Ask for it again and copy the whole line in one go.");
            return false;
        }
    }

    // Codes arrive having been through chat, Discord and web pages, which add line wrapping,
    // smart quotes and invisible formatting characters. Everything before the prefix is dropped
    // and the known decorations are stripped before decoding is attempted.
    private static string Clean(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return string.Empty;

        var start = code.IndexOf(Prefix, StringComparison.OrdinalIgnoreCase);
        if (start > 0)
            code = code[start..];

        var kept = new StringBuilder(code.Length);

        foreach (var c in code)
        {
            if (char.IsWhiteSpace(c) || Decorative(c))
                continue;

            kept.Append(c);
        }

        return kept.ToString();
    }

    private static readonly char[] Decorations =
    [
        '`', '"', '<', '>',
        (char)0x0027,
        (char)0x00a0,
        (char)0x200b,
        (char)0x200c,
        (char)0x200d,
        (char)0xfeff,
    ];

    private static bool Decorative(char c) => Array.IndexOf(Decorations, c) >= 0;

    private static Dictionary<string, object?> Overrides(Configuration settings)
    {
        var defaults = new Configuration();
        var result = new Dictionary<string, object?>();

        foreach (var property in ConfigurationCopy.Settings)
        {
            var mine = property.GetValue(settings);

            if (Equals(mine, property.GetValue(defaults)))
                continue;

            result[property.Name] = mine;
        }

        return result;
    }

    // Absent means a code from before the field existed; every build has written it, so treating
    // a missing one as current keeps a hand-made code from being migrated out of shape.
    private static int VersionOf(JsonElement root) =>
        root.TryGetProperty("v", out var version)
        && version.ValueKind == JsonValueKind.Number
        && version.TryGetInt32(out var value)
            ? value
            : Configuration.CurrentVersion;

    private static void ApplyOverrides(JsonElement overrides, Configuration to)
    {
        foreach (var member in overrides.EnumerateObject())
        {
            if (ConfigurationCopy.FindForImport(member.Name) is not { } property)
                continue;

            try
            {
                var value = member.Value.Deserialize(property.PropertyType, JsonOptions);

                if (IsUndefinedEnum(property.PropertyType, value))
                    continue;

                property.SetValue(to, value);
            }
            catch (Exception)
            {
            }
        }
    }

    // A code from a newer build can name an effect this one has never heard of. Skipping the
    // value rather than storing it keeps the rest of the preset usable instead of leaving an
    // enum holding a number nothing can render.
    private static bool IsUndefinedEnum(Type type, object? value)
    {
        if (value == null)
            return false;

        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying.IsEnum && !Enum.IsDefined(underlying, value);
    }

    // Capped on the way out, not the way in: a short code can inflate to an enormous amount of
    // data, and this is parsing something a stranger pasted.
    private static string? Inflate(byte[] raw)
    {
        using var input = new MemoryStream(raw);
        using var deflate = new DeflateStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();

        var buffer = new byte[4096];
        int read;

        while ((read = deflate.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (output.Length + read > MaxDecompressedBytes)
                return null;

            output.Write(buffer, 0, read);
        }

        return Encoding.UTF8.GetString(output.ToArray());
    }

    private static string ToBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static byte[] FromBase64Url(string text)
    {
        var padded = text.Replace('-', '+').Replace('_', '/');

        return Convert.FromBase64String(padded.PadRight((padded.Length + 3) / 4 * 4, '='));
    }

    private sealed class TicksTimeSpanConverter : JsonConverter<TimeSpan>
    {
        public override TimeSpan Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
            TimeSpan.FromTicks(reader.GetInt64());

        public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options) =>
            writer.WriteNumberValue(value.Ticks);
    }
}
