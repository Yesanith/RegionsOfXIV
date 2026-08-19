using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RegionsOfXIV;

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

    private static void ApplyOverrides(JsonElement overrides, Configuration to)
    {
        foreach (var member in overrides.EnumerateObject())
        {
            if (ConfigurationCopy.Find(member.Name) is not { } property)
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

    private static bool IsUndefinedEnum(Type type, object? value)
    {
        if (value == null)
            return false;

        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying.IsEnum && !Enum.IsDefined(underlying, value);
    }

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
