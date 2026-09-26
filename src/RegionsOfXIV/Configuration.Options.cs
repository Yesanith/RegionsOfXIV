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
// Do not renumber these. The number is what the config file stores, so a setting saved by an
// older build, or a preset carrying one, has to read back as the same source.
public enum SoundSource
{
    Off,
    GameSound,
    File,
}

// The same rule as SoundSource for every enum below: a value's number is what the config file
// and every share code store, so new members are only ever appended.
public enum MotionEffect
{
    None,
    Typewriter,
    Rise,
    Wave,
    Burn,
    Drop,
    Slide,
    Assemble,
    Flicker,
    Zoom,
}

// How a line leaves. Fade is the plain fade the plugin always had; the rest play a motion
// backwards over the fade, so a line goes out the way it came in, scatters, or sinks.
public enum DepartureEffect
{
    Fade,
    Reverse,
    Dissolve,
    Fall,
}

public enum ParticleEffect
{
    None,
    Hearts,
    Embers,
    Sparkles,
    Petals,
    Snow,
    Fireflies,
    Leaves,
    Rain,
    Stars,
}

// What sits behind the lines: nothing, a band the width of the text, or a strip across the
// whole screen.
public enum BackingStyle
{
    None,
    Band,
    Vignette,
}

// A run of colours across the letters of the place name, in place of one text colour.
public enum TextPalette
{
    None,
    Rainbow,
    Sunset,
    Ocean,
    Aurora,
    Blossom,
    Golden,
}

// How a palette moves: laid once across the line, travelling along it, or the whole line
// cycling through it together.
public enum PaletteMotion
{
    Static,
    Wave,
    Pulse,
}
