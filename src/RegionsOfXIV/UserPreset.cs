using System;

namespace RegionsOfXIV;

[Serializable]
// A saved preset is a whole Configuration, copied by value both going in and coming out, so
// editing your live settings never reaches back into a preset you saved earlier.
public class UserPreset
{
    public string Name { get; set; } = string.Empty;

    public Configuration Settings { get; set; } = new();

    public static UserPreset Capture(string name, Configuration config)
    {
        var preset = new UserPreset { Name = name };
        ConfigurationCopy.Apply(config, preset.Settings);
        return preset;
    }

    public void ApplyTo(Configuration config) => ConfigurationCopy.Apply(this.Settings, config);
}
