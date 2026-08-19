using System;
using System.Linq;
using System.Reflection;

namespace RegionsOfXIV;

// A complete saved configuration, under a name.
//
// Everything the General, Effects, Notifications and Durations tabs edit — which
// between them is every setting the plugin has, bar two. Version is excluded
// because it describes the file's shape rather than a preference, and UserPresets
// because a preset cannot sensibly contain the list it lives in.
//
// Copied by reflection rather than property by property, and that is the point: a
// setting added to Configuration later belongs to presets immediately, with no
// second place to remember. Hand-written assignments would silently omit it, and
// the omission would surface much later as "this preset does not restore X".
[Serializable]
public class UserPreset
{
    // Version says which shape the file is in, not what the user prefers, and a
    // preset holding the preset list would nest without end.
    private static readonly string[] NotCopied = ["Version", "UserPresets"];

    public string Name { get; set; } = string.Empty;

    // A nested Configuration rather than a parallel set of fields: it serialises
    // with no extra work and cannot drift out of step with the real one. Its own
    // UserPresets is never written, so it stays empty and nothing recurses.
    public Configuration Settings { get; set; } = new();

    public static UserPreset Capture(string name, Configuration config)
    {
        var preset = new UserPreset { Name = name };
        Copy(config, preset.Settings);
        return preset;
    }

    public void ApplyTo(Configuration config) => Copy(this.Settings, config);

    private static void Copy(Configuration from, Configuration to)
    {
        foreach (var property in typeof(Configuration).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || !property.CanWrite)
                continue;

            if (NotCopied.Contains(property.Name))
                continue;

            property.SetValue(to, property.GetValue(from));
        }
    }
}
