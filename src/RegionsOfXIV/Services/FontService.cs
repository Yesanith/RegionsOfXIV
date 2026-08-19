using System;
using System.IO;
using Dalamud;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;

namespace RegionsOfXIV.Services;

internal sealed class FontService : IDisposable
{
    private readonly IFontAtlas atlas;
    private readonly Configuration config;

    private IFontHandle? displayFont;
    private IFontHandle? headerFont;
    private IFontHandle? eorzeanDisplayFont;

    private float builtDisplaySize;
    private float builtHeaderSize;
    private DisplayFontChoice builtDisplayChoice;

    public FontService(Configuration config)
    {
        this.config = config;
        this.atlas = Plugin.PluginInterface.UiBuilder.FontAtlas;
    }

    public int Generation { get; private set; }

    public IFontHandle Display => this.displayFont ?? Plugin.PluginInterface.UiBuilder.DefaultFontHandle;

    public IFontHandle Header => this.headerFont ?? Plugin.PluginInterface.UiBuilder.DefaultFontHandle;

    public IFontHandle? EorzeanDisplay => this.eorzeanDisplayFont;

    public static float NativeCeilingPx(DisplayFontChoice choice) => choice switch
    {
        DisplayFontChoice.NotoSansCjk => float.PositiveInfinity,
        DisplayFontChoice.Jupiter => 46f * 4f / 3f,
        DisplayFontChoice.Axis => 36f * 4f / 3f,
        _ => 68f * 4f / 3f,
    };

    public static bool IsLatinOnly(DisplayFontChoice choice) =>
        choice is DisplayFontChoice.TrumpGothic or DisplayFontChoice.Jupiter;

    public void Rebuild(float displaySizePx, float headerSizePx)
    {
        var choice = this.config.DisplayFont;

        if (Math.Abs(displaySizePx - this.builtDisplaySize) < 0.5f &&
            Math.Abs(headerSizePx - this.builtHeaderSize) < 0.5f &&
            choice == this.builtDisplayChoice)
            return;

        this.builtDisplaySize = displaySizePx;
        this.builtHeaderSize = headerSizePx;
        this.builtDisplayChoice = choice;

        Generation++;

        DisposeHandles();

        this.displayFont = BuildDisplayFont(choice, displaySizePx);

        this.headerFont = this.atlas.NewGameFontHandle(
            new GameFontStyle(GameFontFamily.Axis, headerSizePx));

        TryBuildEorzean(displaySizePx);
    }

    private IFontHandle BuildDisplayFont(DisplayFontChoice choice, float sizePx) =>
        choice == DisplayFontChoice.NotoSansCjk
            ? this.atlas.NewDelegateFontHandle(e => e.OnPreBuild(tk =>
                tk.AddDalamudAssetFont(
                    DalamudAsset.NotoSansCjkMedium,
                    new SafeFontConfig { SizePx = sizePx, GlyphRanges = JapaneseGlyphRanges() })))
            : this.atlas.NewGameFontHandle(new GameFontStyle(ResolveGameFamily(choice), sizePx));

    private static GameFontFamily ResolveGameFamily(DisplayFontChoice choice) => choice switch
    {
        DisplayFontChoice.Jupiter => GameFontFamily.Jupiter,
        DisplayFontChoice.Axis => GameFontFamily.Axis,
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

    private void TryBuildEorzean(float displaySizePx)
    {
        var path = FindBundledFont();
        if (path == null)
        {
            Plugin.Log.Debug("No Eorzean font bundled; decode effect will fall back to a plain fade.");
            return;
        }

        try
        {
            this.eorzeanDisplayFont = this.atlas.NewDelegateFontHandle(
                e => e.OnPreBuild(tk => tk.AddFontFromFile(path, new SafeFontConfig { SizePx = displaySizePx })));

            Plugin.Log.Information($"Loaded Eorzean font from {path}");
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, $"Failed to load bundled Eorzean font from {path}");
            this.eorzeanDisplayFont = null;
        }
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

    private void DisposeHandles()
    {
        this.displayFont?.Dispose();
        this.headerFont?.Dispose();
        this.eorzeanDisplayFont?.Dispose();

        this.displayFont = null;
        this.headerFont = null;
        this.eorzeanDisplayFont = null;
    }

    public void Dispose() => DisposeHandles();
}
