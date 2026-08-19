using System;

namespace RegionsOfXIV;

[Serializable]
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
