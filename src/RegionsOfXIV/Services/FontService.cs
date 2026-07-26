using System;
using System.IO;
using Dalamud.Game;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;

namespace RegionsOfXIV.Services;

// The game's own UI fonts are never bundled — Dalamud builds ImGui fonts straight
// from the game files via GameFontFamily (Axis is the general UI face,
// TrumpGothic the narrow title face). Only fonts we ship ourselves, i.e. the
// Eorzean alphabet used by the reveal effect, are loaded from disk.
// See assets/fonts/README.md for the full family list.
internal sealed class FontService : IDisposable
{
    private readonly IFontAtlas atlas;
    private readonly Configuration config;

    private IFontHandle? displayFont;
    private IFontHandle? headerFont;
    private IFontHandle? eorzeanDisplayFont;
    private IFontHandle? eorzeanHeaderFont;

    private float builtDisplaySize;
    private float builtHeaderSize;
    private GameFontFamily builtDisplayFamily;
    private bool builtDalamudFace;

    public FontService(Configuration config)
    {
        this.config = config;
        this.atlas = Plugin.PluginInterface.UiBuilder.FontAtlas;
    }

    public IFontHandle Display => this.displayFont ?? Plugin.PluginInterface.UiBuilder.DefaultFontHandle;

    public IFontHandle Header => this.headerFont ?? Plugin.PluginInterface.UiBuilder.DefaultFontHandle;

    public IFontHandle? EorzeanDisplay => this.eorzeanDisplayFont;

    public IFontHandle? EorzeanHeader => this.eorzeanHeaderFont;

    public bool HasEorzeanFont => this.eorzeanDisplayFont != null;

    // Game fonts are bitmap atlases baked at fixed sizes; asking for more than the
    // largest one upscales the bitmap and softens the result. These are the
    // ceilings in pixels, derived from the largest .fdt each family ships
    // (GameFontStyle converts px to pt as px * 3/4, so px = pt * 4/3).
    public static float NativeCeilingPx(DisplayFontChoice choice) => choice switch
    {
        DisplayFontChoice.Axis => 36f * 4f / 3f,          // AXIS_36  -> 48 px
        DisplayFontChoice.Jupiter => 46f * 4f / 3f,       // Jupiter_46 -> ~61 px
        DisplayFontChoice.TrumpGothic => 68f * 4f / 3f,   // TrumpGothic_68 -> ~91 px
        DisplayFontChoice.Dalamud => float.MaxValue,      // vector, no ceiling
        _ => float.MaxValue,
    };

    // The ceiling for whatever Auto currently resolves to.
    public float EffectiveCeilingPx => NativeCeilingPx(
        this.config.DisplayFont == DisplayFontChoice.Auto
            ? (ResolveDisplayFamily() == GameFontFamily.Axis
                ? DisplayFontChoice.Axis
                : DisplayFontChoice.TrumpGothic)
            : this.config.DisplayFont);

    // Rebuilds the atlas handles. Never call from the draw path.
    public void Rebuild(float displaySizePx, float headerSizePx)
    {
        var family = ResolveDisplayFamily();
        var useDalamudFace = this.config.DisplayFont == DisplayFontChoice.Dalamud;

        if (Math.Abs(displaySizePx - this.builtDisplaySize) < 0.5f &&
            Math.Abs(headerSizePx - this.builtHeaderSize) < 0.5f &&
            family == this.builtDisplayFamily &&
            useDalamudFace == this.builtDalamudFace)
            return;

        this.builtDisplaySize = displaySizePx;
        this.builtHeaderSize = headerSizePx;
        this.builtDisplayFamily = family;
        this.builtDalamudFace = useDalamudFace;

        DisposeHandles();

        this.displayFont = useDalamudFace
            ? this.atlas.NewDelegateFontHandle(
                e => e.OnPreBuild(tk => tk.AddDalamudDefaultFont(displaySizePx)))
            : this.atlas.NewGameFontHandle(new GameFontStyle(family, displaySizePx));

        // Axis for the header regardless — it is the only family with Japanese
        // glyphs, and the header is small enough that the narrow display faces
        // buy nothing.
        this.headerFont = this.atlas.NewGameFontHandle(
            new GameFontStyle(GameFontFamily.Axis, headerSizePx));

        TryBuildEorzean(displaySizePx, headerSizePx);
    }

    // TrumpGothic and Jupiter are Latin-only. Picking either on a Japanese client
    // renders the zone name as tofu, so Auto falls back to Axis there.
    private GameFontFamily ResolveDisplayFamily() => this.config.DisplayFont switch
    {
        DisplayFontChoice.TrumpGothic => GameFontFamily.TrumpGothic,
        DisplayFontChoice.Jupiter => GameFontFamily.Jupiter,
        DisplayFontChoice.Axis => GameFontFamily.Axis,
        DisplayFontChoice.Dalamud => GameFontFamily.Undefined,
        _ => Plugin.ClientState.ClientLanguage == ClientLanguage.Japanese
            ? GameFontFamily.Axis
            : GameFontFamily.TrumpGothic,
    };

    // Absence of a bundled font is not an error — the reveal effect degrades to a
    // plain fade.
    private void TryBuildEorzean(float displaySizePx, float headerSizePx)
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
            this.eorzeanHeaderFont = this.atlas.NewDelegateFontHandle(
                e => e.OnPreBuild(tk => tk.AddFontFromFile(path, new SafeFontConfig { SizePx = headerSizePx })));

            Plugin.Log.Information($"Loaded Eorzean font from {path}");
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, $"Failed to load bundled Eorzean font from {path}");
            this.eorzeanDisplayFont = null;
            this.eorzeanHeaderFont = null;
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
        this.eorzeanHeaderFont?.Dispose();

        this.displayFont = null;
        this.headerFont = null;
        this.eorzeanDisplayFont = null;
        this.eorzeanHeaderFont = null;
    }

    public void Dispose() => DisposeHandles();
}
