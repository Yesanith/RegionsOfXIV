using System;
using System.IO;
using System.Linq;
using Dalamud;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;

namespace RegionsOfXIV.Services;

// Owns one built font per FontRole -- text, header, weather -- and rebuilds only the roles whose
// face, size or file actually changed, because a rebuild throws away the whole Dalamud font atlas
// and is far too expensive to do on every frame a slider is being dragged.
internal sealed class FontService : IDisposable
{
    private const FontChoice FallbackChoice = FontChoice.NotoSansCjk;

    private const string CouldNotLoad =
        "That file could not be read as a font, so the plugin is drawing with Noto Sans CJK instead.";

    private static readonly FontRole[] Roles = Enum.GetValues<FontRole>();

    private readonly IFontAtlas atlas;
    private readonly Configuration config;
    private readonly Face[] faces;

    private bool builtWithDecoding;

    public FontService(Configuration config)
    {
        this.config = config;
        this.atlas = Plugin.PluginInterface.UiBuilder.FontAtlas;
        this.faces = Roles.Select(_ => new Face()).ToArray();
    }

    // Bumped whenever any face is rebuilt. LineLayout keys its caches on this, so a font change
    // invalidates every measurement taken against the old glyphs.
    public int Generation { get; private set; }

    public IFontHandle Display => Plain(FontRole.Text);

    public IFontHandle Header => Plain(FontRole.Header);

    public IFontHandle Weather => Plain(FontRole.Weather);

    public IFontHandle? EorzeanDisplay => this.faces[(int)FontRole.Text].Eorzean;

    public IFontHandle? EorzeanWeather => this.faces[(int)FontRole.Weather].Eorzean;

    public string? ProblemWith(FontRole role)
    {
        var face = this.faces[(int)role];

        if (face.Problem != null)
            return face.Problem;

        return face.Plain?.LoadException == null ? null : CouldNotLoad;
    }

    public void Rebuild()
    {
        var rebuilt = false;

        // The decode effect is not part of a face's identity -- choice, path and size are -- but it
        // decides whether the Eorzean handle gets built at all. So a change to it has to force every
        // role through Build even though Matches would happily say nothing moved.
        var decoding = this.config.DecodeEffectEnabled;
        var decodingChanged = decoding != this.builtWithDecoding;
        this.builtWithDecoding = decoding;

        foreach (var role in Roles)
        {
            var wanted = this.config.FontFor(role);
            var face = this.faces[(int)role];

            if (!decodingChanged && face.Matches(wanted))
                continue;

            Build(role, face, wanted);
            rebuilt = true;
        }

        if (rebuilt)
            Generation++;
    }

    // A custom font that fails to load leaves Plain unusable. Falling through to the stock face
    // built alongside it keeps the notification readable; pushing the failed handle would silently
    // give us the small default UI font instead, which looks broken rather than degraded.
    private IFontHandle Plain(FontRole role)
    {
        var face = this.faces[(int)role];

        if (face.Plain is { LoadException: null } handle)
            return handle;

        return face.Fallback ?? Plugin.PluginInterface.UiBuilder.DefaultFontHandle;
    }

    // The stored size is left exactly as the player set it. Only what reaches the atlas is held
    // down, so switching the game's language back gives them their size again without anything
    // having been rewritten behind their back.
    private void Build(FontRole role, Face face, FontSetting wanted)
    {
        face.Release();

        // Recorded before the clamp, not after. Matches compares this against what the config
        // asks for, so holding the clamped size here would mismatch on every frame and rebuild
        // the atlas forever.
        face.Built = wanted;

        var ceiling = FontLimits.MaxAffordablePx(role);

        if (wanted.SizePx > ceiling)
        {
            face.ReducedTo = ceiling;
            wanted = wanted with { SizePx = ceiling };
        }

        // Only custom roles get a fallback built, since a stock face cannot fail to load.
        if (wanted.IsCustom)
        {
            face.Problem = FontLimits.CustomFontProblem(wanted.Path);
            face.Fallback = BuildStock(FallbackChoice, wanted.SizePx);

            if (face.Problem == null)
                face.Plain = BuildCustom(face, wanted.Path, wanted.SizePx);
        }
        else
        {
            face.Plain = BuildStock(wanted.Choice, wanted.SizePx);
        }

        // Only while the decode effect is on, since it is the only thing that draws with this
        // handle. Built unconditionally it cost three faces' worth of atlas for every player with
        // the effect off.
        //
        // Default ranges on purpose. The Eorzean font is a Latin-only recreation of the in-game
        // alphabet, so asking it for kana and kanji would reserve thousands of glyphs it does not
        // contain and cost atlas space for nothing.
        //
        // Text and weather only. The header is painted by DrawHeader as a plain run rather than
        // through a FontPair, so it never scrambles and a face built for it is atlas nothing ever
        // reads from. If the header is ever given the effect too, this is the line that has to
        // know about it.
        face.Eorzean = this.config.DecodeEffectEnabled && role != FontRole.Header
            ? BuildFromFile(BundledEorzeanPath(), wanted.SizePx, glyphRanges: null)
            : null;
    }

    private IFontHandle BuildStock(FontChoice choice, float sizePx) =>
        choice == FontChoice.NotoSansCjk
            ? this.atlas.NewDelegateFontHandle(e => e.OnPreBuild(tk =>
                tk.AddDalamudAssetFont(
                    DalamudAsset.NotoSansCjkMedium,
                    new SafeFontConfig { SizePx = sizePx, GlyphRanges = FontLimits.NotificationGlyphRanges() })))
            : this.atlas.NewGameFontHandle(new GameFontStyle(ResolveGameFamily(choice), sizePx));

    private IFontHandle? BuildCustom(Face face, string path, float sizePx)
    {
        // The same ranges the stock face gets. Without them ImGui builds its default set, which is
        // Latin-1 only, and a player on the Japanese client who picks a custom font gets blanks for
        // every place name with nothing anywhere saying why.
        var handle = BuildFromFile(path, sizePx, FontLimits.NotificationGlyphRanges());

        if (handle == null)
            face.Problem = CouldNotLoad;

        return handle;
    }

    // Ranges are the caller's to state, and the two callers want opposite things -- so there is no
    // default: anything new calling this has to decide which it is.
    private IFontHandle? BuildFromFile(string? path, float sizePx, ushort[]? glyphRanges)
    {
        if (path == null)
            return null;

        try
        {
            return this.atlas.NewDelegateFontHandle(
                e => e.OnPreBuild(tk => tk.AddFontFromFile(
                    path, new SafeFontConfig { SizePx = sizePx, GlyphRanges = glyphRanges })));
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

        // Separate from Problem, which is only ever drawn on the custom-font path and is coloured
        // as a fault. This one is neither: the face loaded, it is simply not the size that was
        // asked for, and it applies to a stock face just as much as a custom one.
        public float? ReducedTo;

        public FontSetting? Built;

        // Half a pixel of slack: a size slider passes through many intermediate values on the
        // way to where it is going, and none of them are worth an atlas rebuild.
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
            this.ReducedTo = null;
            this.Built = null;
        }
    }
}
