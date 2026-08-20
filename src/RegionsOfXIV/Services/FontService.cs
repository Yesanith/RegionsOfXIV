using System;
using System.IO;
using System.Linq;
using Dalamud;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;

namespace RegionsOfXIV.Services;

internal sealed class FontService : IDisposable
{
    public const long MaxCustomFontBytes = 64L * 1024 * 1024;

    public static readonly string[] CustomFontExtensions = [".ttf", ".otf", ".ttc"];

    public const FontChoice FallbackChoice = FontChoice.NotoSansCjk;

    private const string CouldNotLoad =
        "That file could not be read as a font, so the plugin is drawing with Noto Sans CJK instead.";

    private static readonly FontRole[] Roles = Enum.GetValues<FontRole>();

    private readonly IFontAtlas atlas;
    private readonly Configuration config;
    private readonly Face[] faces;

    public FontService(Configuration config)
    {
        this.config = config;
        this.atlas = Plugin.PluginInterface.UiBuilder.FontAtlas;
        this.faces = Roles.Select(_ => new Face()).ToArray();
    }

    public int Generation { get; private set; }

    public IFontHandle Display => Plain(FontRole.Text);

    public IFontHandle Header => Plain(FontRole.Header);

    public IFontHandle Weather => Plain(FontRole.Weather);

    public IFontHandle? EorzeanDisplay => this.faces[(int)FontRole.Text].Eorzean;

    public IFontHandle? EorzeanHeader => this.faces[(int)FontRole.Header].Eorzean;

    public IFontHandle? EorzeanWeather => this.faces[(int)FontRole.Weather].Eorzean;

    public string? ProblemWith(FontRole role)
    {
        var face = this.faces[(int)role];

        if (face.Problem != null)
            return face.Problem;

        return face.Plain?.LoadException == null ? null : CouldNotLoad;
    }

    public static float NativeCeilingPx(FontChoice choice) => choice switch
    {
        FontChoice.NotoSansCjk => float.PositiveInfinity,
        FontChoice.Custom => float.PositiveInfinity,
        FontChoice.Jupiter => 46f * 4f / 3f,
        FontChoice.Axis => 36f * 4f / 3f,
        _ => 68f * 4f / 3f,
    };

    public static bool IsLatinOnly(FontChoice choice) =>
        choice is FontChoice.TrumpGothic or FontChoice.Jupiter;

    public static string? CustomFontProblem(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "No font file has been chosen yet.";

        var extension = Path.GetExtension(path);

        if (!CustomFontExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            return "Only .ttf, .otf and .ttc files can be used as fonts.";

        FileInfo file;

        try
        {
            file = new FileInfo(path);
        }
        catch (Exception)
        {
            return "That path cannot be read.";
        }

        if (!file.Exists)
            return "There is no file at that path any more.";

        if (file.Length == 0)
            return "That file is empty.";

        if (file.Length > MaxCustomFontBytes)
            return "That file is far larger than any font, so it is being left alone.";

        return null;
    }

    public void Rebuild()
    {
        var rebuilt = false;

        foreach (var role in Roles)
        {
            var wanted = this.config.FontFor(role);
            var face = this.faces[(int)role];

            if (face.Matches(wanted))
                continue;

            Build(face, wanted);
            rebuilt = true;
        }

        if (rebuilt)
            Generation++;
    }

    private IFontHandle Plain(FontRole role)
    {
        var face = this.faces[(int)role];

        if (face.Plain is { LoadException: null } handle)
            return handle;

        return face.Fallback ?? Plugin.PluginInterface.UiBuilder.DefaultFontHandle;
    }

    private void Build(Face face, FontSetting wanted)
    {
        face.Release();
        face.Built = wanted;

        if (wanted.IsCustom)
        {
            face.Problem = CustomFontProblem(wanted.Path);
            face.Fallback = BuildStock(FallbackChoice, wanted.SizePx);

            if (face.Problem == null)
                face.Plain = BuildCustom(face, wanted.Path, wanted.SizePx);
        }
        else
        {
            face.Plain = BuildStock(wanted.Choice, wanted.SizePx);
        }

        face.Eorzean = BuildFromFile(BundledEorzeanPath(), wanted.SizePx);
    }

    private IFontHandle BuildStock(FontChoice choice, float sizePx) =>
        choice == FontChoice.NotoSansCjk
            ? this.atlas.NewDelegateFontHandle(e => e.OnPreBuild(tk =>
                tk.AddDalamudAssetFont(
                    DalamudAsset.NotoSansCjkMedium,
                    new SafeFontConfig { SizePx = sizePx, GlyphRanges = JapaneseGlyphRanges() })))
            : this.atlas.NewGameFontHandle(new GameFontStyle(ResolveGameFamily(choice), sizePx));

    private IFontHandle? BuildCustom(Face face, string path, float sizePx)
    {
        var handle = BuildFromFile(path, sizePx);

        if (handle == null)
            face.Problem = CouldNotLoad;

        return handle;
    }

    private IFontHandle? BuildFromFile(string? path, float sizePx)
    {
        if (path == null)
            return null;

        try
        {
            return this.atlas.NewDelegateFontHandle(
                e => e.OnPreBuild(tk => tk.AddFontFromFile(path, new SafeFontConfig { SizePx = sizePx })));
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Could not load the font at {path}");
            return null;
        }
    }

    private static GameFontFamily ResolveGameFamily(FontChoice choice) => choice switch
    {
        FontChoice.Jupiter => GameFontFamily.Jupiter,
        FontChoice.Axis => GameFontFamily.Axis,
        _ => GameFontFamily.TrumpGothic,
    };

    private static ushort[]? CachedJapaneseGlyphRanges;

    private static unsafe ushort[] JapaneseGlyphRanges()
    {
        if (CachedJapaneseGlyphRanges != null)
            return CachedJapaneseGlyphRanges;

        var source = ImGui.GetIO().Fonts.GetGlyphRangesJapanese();

        var length = 0;
        while (source[length] != 0)
            length++;

        var ranges = new ushort[length + 1];
        for (var i = 0; i < length; i++)
            ranges[i] = source[i];

        return CachedJapaneseGlyphRanges = ranges;
    }

    private static string? CachedEorzeanPath;
    private static bool SearchedForEorzean;

    private static string? BundledEorzeanPath()
    {
        if (SearchedForEorzean)
            return CachedEorzeanPath;

        SearchedForEorzean = true;
        CachedEorzeanPath = FindBundledFont();

        if (CachedEorzeanPath == null)
            Log.Debug("No Eorzean font bundled; the decode effect will fall back to a plain fade.");
        else
            Log.Information($"Loaded Eorzean font from {CachedEorzeanPath}");

        return CachedEorzeanPath;
    }

    private static string? FindBundledFont()
    {
        var dir = Plugin.PluginInterface.AssemblyLocation.Directory;
        if (dir == null)
            return null;

        var fontDir = Path.Combine(dir.FullName, "Fonts");
        if (!Directory.Exists(fontDir))
            return null;

        foreach (var pattern in new[] { "*eorzea*.ttf", "*eorzea*.otf", "*.ttf", "*.otf" })
        {
            var hit = Directory.GetFiles(fontDir, pattern);
            if (hit.Length > 0)
                return hit[0];
        }

        return null;
    }

    public void Dispose()
    {
        foreach (var face in this.faces)
            face.Release();
    }

    private sealed class Face
    {
        public IFontHandle? Plain;

        public IFontHandle? Fallback;

        public IFontHandle? Eorzean;

        public string? Problem;

        public FontSetting? Built;

        public bool Matches(in FontSetting wanted) =>
            this.Built is { } built
            && built.Choice == wanted.Choice
            && built.Path == wanted.Path
            && Math.Abs(built.SizePx - wanted.SizePx) < 0.5f;

        public void Release()
        {
            this.Plain?.Dispose();
            this.Fallback?.Dispose();
            this.Eorzean?.Dispose();

            this.Plain = null;
            this.Fallback = null;
            this.Eorzean = null;
            this.Problem = null;
            this.Built = null;
        }
    }
}
