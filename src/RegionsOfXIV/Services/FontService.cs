using System;
using System.IO;
using Dalamud;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;

namespace RegionsOfXIV.Services;

// The game's own UI fonts are never bundled — Dalamud builds ImGui fonts straight
// from the game files via GameFontFamily (Axis is the general UI face,
// TrumpGothic the narrow title face). Only fonts we ship ourselves, i.e. the
// Eorzean alphabet used by the reveal effect, are loaded from disk — see NOTICE
// for where that one came from and why it is not under this project's licence.
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

    // Bumped whenever the handles are replaced.
    //
    // Anything that caches a measurement needs to know when the thing it measured
    // against stopped existing. The size alone will not do: two families at one
    // size measure differently, so a cache keyed on size would happily keep
    // Jupiter's advances after a switch to Axis.
    public int Generation { get; private set; }

    public IFontHandle Display => this.displayFont ?? Plugin.PluginInterface.UiBuilder.DefaultFontHandle;

    public IFontHandle Header => this.headerFont ?? Plugin.PluginInterface.UiBuilder.DefaultFontHandle;

    // Only the display line decodes; the header is small enough that the effect
    // would not read, so no Eorzean handle is built at header size.
    public IFontHandle? EorzeanDisplay => this.eorzeanDisplayFont;

    // Game fonts are bitmap atlases baked at fixed sizes; asking for more than the
    // largest one upscales the bitmap and softens the result. These are the
    // ceilings in pixels, derived from the largest .fdt each family ships
    // (GameFontStyle converts px to pt as px * 3/4, so px = pt * 4/3).
    //
    // Noto is vector and has no ceiling at all. Infinity rather than a large
    // number, so the caller's "size > ceiling" test can never fire for it without
    // the UI needing to know which fonts are special.
    //
    // The default arm matters: a config written by an older build can hold a value
    // no longer in the enum, and Newtonsoft deserializes that without complaint.
    public static float NativeCeilingPx(DisplayFontChoice choice) => choice switch
    {
        DisplayFontChoice.NotoSansCjk => float.PositiveInfinity,
        DisplayFontChoice.Jupiter => 46f * 4f / 3f,   // Jupiter_46 -> ~61 px
        DisplayFontChoice.Axis => 36f * 4f / 3f,      // AXIS_36 -> 48 px
        _ => 68f * 4f / 3f,                           // TrumpGothic_68 -> ~91 px
    };

    // Whether a face will render Japanese as blank boxes. A fact about the font
    // rather than a UI concern, so it belongs here — the config window only decides
    // how loudly to say it.
    public static bool IsLatinOnly(DisplayFontChoice choice) =>
        choice is DisplayFontChoice.TrumpGothic or DisplayFontChoice.Jupiter;

    // Rebuilds the atlas handles. Never call from the draw path.
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

        // After the early-out above, so a call that changes nothing does not
        // invalidate every cached measurement in the plugin.
        Generation++;

        DisposeHandles();

        this.displayFont = BuildDisplayFont(choice, displaySizePx);

        // Axis for the header regardless — it is the only game family with Japanese
        // glyphs, and the header is small enough that the display faces buy nothing.
        this.headerFont = this.atlas.NewGameFontHandle(
            new GameFontStyle(GameFontFamily.Axis, headerSizePx));

        TryBuildEorzean(displaySizePx);
    }

    // Noto comes from Dalamud's shipped assets and is vector, so it is crisp at any
    // size and covers every language. The other three are the game's own bitmap
    // atlases: they look like FFXIV, at the cost of a ceiling apiece.
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

    // Bounding the range is not optional. SafeFontConfig.GlyphRanges left null
    // means "every glyph in the file within UCS-2", and Noto Sans CJK holds tens of
    // thousands — rasterised at display size, and rebuilt every time the size
    // slider moves. That is a very large texture and a visible stutter on the one
    // interaction where it would be most noticed.
    //
    // ImGui already curates exactly the subset wanted here: Latin, kana, the ~3000
    // common-use kanji and the fullwidth forms, well short of the whole of CJK
    // Unified Ideographs. Reusing its table beats maintaining a Unicode range list
    // by hand. It hands back a pointer into static ImGui memory, so this copies it
    // once into the managed array SafeFontConfig wants, and caches the result.
    private static ushort[]? CachedJapaneseGlyphRanges;

    private static unsafe ushort[] JapaneseGlyphRanges()
    {
        if (CachedJapaneseGlyphRanges != null)
            return CachedJapaneseGlyphRanges;

        var source = ImGui.GetIO().Fonts.GetGlyphRangesJapanese();

        // A zero-terminated run of inclusive from/to pairs.
        var length = 0;
        while (source[length] != 0)
            length++;

        // One longer than the payload, leaving the terminating zero in place.
        var ranges = new ushort[length + 1];
        for (var i = 0; i < length; i++)
            ranges[i] = source[i];

        return CachedJapaneseGlyphRanges = ranges;
    }

    // Absence of a bundled font is not an error — the reveal effect degrades to a
    // plain fade.
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

    // Fonts land in Fonts/ next to the DLL — see the Content item group in
    // RegionsOfXIV.csproj, which links assets/fonts there.
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
