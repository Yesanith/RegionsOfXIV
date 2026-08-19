using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RegionsOfXIV;

// Turning a preset into a line of text someone can paste into chat, and back.
//
// What goes in is only the settings that differ from the defaults. That is not a
// size optimisation first — it is the same rule the built-in presets follow, that
// a preset is the defaults plus what makes it itself. It does keep the codes
// short, which matters when the medium is a Discord message: a typical look is a
// dozen fields, so a couple of hundred characters rather than a couple of
// thousand.
//
// It also makes codes survive this plugin growing. A setting added later is
// absent from an old code and lands on its new default, which is the right answer
// and needs no migration step.
//
// System.Text.Json rather than the Newtonsoft that Dalamud uses for the config
// file: this is a wire format read from strangers, and the BCL serialiser is the
// one whose version cannot be argued over by whatever else is loaded.
//
// Nothing here logs. Every failure is returned as a sentence for the caller to
// show, which keeps the codec free of Plugin and therefore testable off a live
// game — the codec is precisely the part that has to be right before it ships.
//
// Shape: "ROX1-" + url-safe base64 of deflate of
//     {"n": <name>, "v": <config version>, "s": {<setting>: <value>, ...}}
internal static class PresetCode
{
    // The digit is the code format's own version, not the configuration's — it
    // changes only if this encoding does. It doubles as something to recognise a
    // code by when it arrives surrounded by conversation.
    private const string Prefix = "ROX1-";

    // A pasted code is untrusted input, and deflate happily turns a few hundred
    // bytes into gigabytes. Both ends are capped: the text before it is decoded,
    // and the output as it is being decompressed.
    private const int MaxCodeLength = 8 * 1024;
    private const int MaxDecompressedBytes = 64 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        // Vector4 keeps X/Y/Z/W as fields, not properties, and would otherwise
        // serialise as an empty object — every colour silently black on import.
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

        // Closed before ToArray: deflate holds a partial block until it is, and
        // reading the stream early yields a truncated code that decodes to nothing.
        using (var deflate = new DeflateStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
            deflate.Write(json, 0, json.Length);

        return Prefix + ToBase64Url(compressed.ToArray());
    }

    // False rather than throwing: every failure here is a person pasting the wrong
    // thing, which is ordinary and wants a sentence rather than a stack trace. The
    // message is written to be shown in the config window as it stands.
    public static bool TryDecode(string code, out UserPreset? preset, out string error)
    {
        preset = null;

        var trimmed = (code ?? string.Empty).Trim();

        if (trimmed.Length == 0)
        {
            error = "There is nothing on the clipboard to import.";
            return false;
        }

        if (trimmed.Length > MaxCodeLength)
        {
            error = "That code is too long to be one of ours.";
            return false;
        }

        if (!trimmed.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            error = $"That does not look like a preset code — they start with \"{Prefix}\".";
            return false;
        }

        try
        {
            var json = Inflate(FromBase64Url(trimmed[Prefix.Length..]));

            if (json == null)
            {
                error = "That code did not unpack to anything readable.";
                return false;
            }

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "That code did not unpack to a preset.";
                return false;
            }

            var name = root.TryGetProperty("n", out var nameElement) && nameElement.ValueKind == JsonValueKind.String
                ? nameElement.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(name))
            {
                error = "That code carries no preset name.";
                return false;
            }

            // Starts from a fresh Configuration, so every setting the code leaves
            // out is already at its default before the overrides land.
            var built = new UserPreset { Name = name.Trim() };

            if (root.TryGetProperty("s", out var overrides) && overrides.ValueKind == JsonValueKind.Object)
                ApplyOverrides(overrides, built.Settings);

            preset = built;
            error = string.Empty;
            return true;
        }
        catch (Exception)
        {
            error = "That code is damaged — it may have been cut short when it was copied.";
            return false;
        }
    }

    // Only what differs from a freshly constructed Configuration.
    private static Dictionary<string, object?> Overrides(Configuration settings)
    {
        var defaults = new Configuration();
        var result = new Dictionary<string, object?>();

        foreach (var property in ConfigurationCopy.Settings)
        {
            var mine = property.GetValue(settings);

            // Equals rather than ==: these are boxed, and == on object would compare
            // references and report every value type as different.
            if (Equals(mine, property.GetValue(defaults)))
                continue;

            result[property.Name] = mine;
        }

        return result;
    }

    private static void ApplyOverrides(JsonElement overrides, Configuration to)
    {
        foreach (var member in overrides.EnumerateObject())
        {
            // A setting this build does not have. Skipped rather than refused: the
            // rest of the code is still a perfectly good preset, and the alternative
            // is that updating the plugin on one machine breaks sharing with every
            // machine that has not updated yet.
            if (ConfigurationCopy.Find(member.Name) is not { } property)
                continue;

            try
            {
                var value = member.Value.Deserialize(property.PropertyType, JsonOptions);

                // A motion or particle this build has never heard of, which is what
                // a code written by a newer version looks like from here.
                //
                // Worth catching explicitly, because nothing else would. Enums go
                // over the wire as numbers, and the deserialiser will hand back any
                // number the type can physically hold without checking that it
                // names anything — so this would be stored, saved, and then shown in
                // the combo as a bare "5" that animates as nothing. Skipping leaves
                // the setting at its default, which is the same thing an unknown
                // setting name above does.
                if (IsUndefinedEnum(property.PropertyType, value))
                    continue;

                property.SetValue(to, value);
            }
            catch (Exception)
            {
                // One unreadable value, not one unreadable code — a string where an
                // enum belongs costs that setting its default and nothing else.
            }
        }
    }

    // None of the enums here are [Flags]; a combination would not be "defined" and
    // this would reject it. Revisit if one ever is.
    private static bool IsUndefinedEnum(Type type, object? value)
    {
        if (value == null)
            return false;

        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying.IsEnum && !Enum.IsDefined(underlying, value);
    }

    // Null when the payload exceeds the cap, which is the shape a decompression
    // bomb arrives in.
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

    // Base64's "+" and "/" are legal but awkward in a chat message: "/" reads as a
    // path and a trailing "=" invites someone to trim it. The url-safe alphabet
    // with padding dropped makes a code one unbroken token that survives being
    // double-clicked.
    private static string ToBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static byte[] FromBase64Url(string text)
    {
        var padded = text.Replace('-', '+').Replace('_', '/');

        // Base64 decodes in blocks of four; the padding dropped above has to come
        // back before anything will read it.
        return Convert.FromBase64String(padded.PadRight((padded.Length + 3) / 4 * 4, '='));
    }

    // Ticks rather than whatever the BCL's own TimeSpan handling does this release.
    // Exact, compact once deflated, and pinned here where both ends can see it —
    // the durations are the settings a wrong round-trip would corrupt most quietly.
    private sealed class TicksTimeSpanConverter : JsonConverter<TimeSpan>
    {
        public override TimeSpan Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
            TimeSpan.FromTicks(reader.GetInt64());

        public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options) =>
            writer.WriteNumberValue(value.Ticks);
    }
}
