using System;

namespace RegionsOfXIV;

// A complete saved configuration, under a name.
//
// Everything the General, Effects, Notifications and Durations tabs edit — which
// between them is every setting the plugin has, bar the two ConfigurationCopy
// leaves out.
[Serializable]
public class UserPreset
{
    public string Name { get; set; } = string.Empty;

    // A nested Configuration rather than a parallel set of fields: it serialises
    // with no extra work and cannot drift out of step with the real one. Its own
    // UserPresets is never written, so it stays empty and nothing recurses.
    public Configuration Settings { get; set; } = new();

    public static UserPreset Capture(string name, Configuration config)
    {
        var preset = new UserPreset { Name = name };
        ConfigurationCopy.Apply(config, preset.Settings);
        return preset;
    }

    // No reset first, unlike a built-in: the snapshot already holds every setting,
    // so copying it over is a complete replacement on its own.
    public void ApplyTo(Configuration config) => ConfigurationCopy.Apply(this.Settings, config);
}
