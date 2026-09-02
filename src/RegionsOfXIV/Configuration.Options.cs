namespace RegionsOfXIV;

public enum FontChoice
{
    TrumpGothic,
    Jupiter,
    Axis,
    NotoSansCjk,
    Custom,
}

public enum FontRole
{
    Text,
    Header,
    Weather,
}

// One role's font, as a value. Everything that reads or writes a font goes through this rather
// than the nine flat properties behind it in Configuration.
//
// Path is normalised in both the constructor and the init setter, because a config written by
// hand or an older preset can carry a null where a path is expected.
public readonly record struct FontSetting(FontChoice Choice, string Path, float SizePx)
{
    private readonly string path = Path ?? string.Empty;

    public string Path
    {
        get => this.path;
        init => this.path = value ?? string.Empty;
    }

    public bool IsCustom => this.Choice == FontChoice.Custom;
}

// Where a notification's sound comes from.
//
// File is declared but not yet reachable: nothing offers it and NotificationSounds treats it as
// silence. It is here so that the value 2 is spoken for now rather than later, which is what keeps
// adding custom files a matter of writing the playback rather than of migrating everyone's stored
// number. Do not renumber these.
public enum SoundSource
{
    Off,
    GameSound,
    File,
}

public enum MotionEffect
{
    None,
    Typewriter,
    Rise,
    Wave,
    Burn,
}

public enum ParticleEffect
{
    None,
    Hearts,
    Embers,
    Sparkles,
    Petals,
}
