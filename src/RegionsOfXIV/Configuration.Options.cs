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
