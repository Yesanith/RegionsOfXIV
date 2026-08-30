using System;
using System.IO;
using System.Linq;
using Dalamud;
using Dalamud.Bindings.ImGui;
using Dalamud.Game;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;

namespace RegionsOfXIV.Services;

// Owns one built font per FontRole -- text, header, weather -- and rebuilds only the roles whose
// face, size or file actually changed, because a rebuild throws away the whole Dalamud font atlas
// and is far too expensive to do on every frame a slider is being dragged.
internal sealed class FontService : IDisposable
{
    private const long MaxCustomFontBytes = 64L * 1024 * 1024;

    private static readonly string[] CustomFontExtensions = [".ttf", ".otf", ".ttc"];

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

    // The largest size this client can afford to build a notification face at, in pixels before
    // the atlas applies the global scale. It depends on the glyph set rather than on the role or
    // the typeface: a client that needs kanji is asking for ten times the glyphs, and atlas cost
    // is the glyph count times the square of the size.
    //
    // The Fonts tab offers this as the slider maximum, and Build holds to it as well, because a
    // config can carry a larger number from a time when the client was not Japanese. Honouring
    // that number would mean a 352 MB atlas where the same settings cost 16 MB in English.
    public static float MaxAffordablePx(FontRole role) => role == FontRole.Text
        ? (NeedsCjkGlyphs ? 140f : 280f)
        : (NeedsCjkGlyphs ? 72f : 144f);

    // Checked before the file is handed to the atlas, because a bad path fails asynchronously
    // inside the build and surfaces as a blank line rather than an error anyone can act on.
    // Callers should cache the result -- it touches the disk.
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

        var ceiling = MaxAffordablePx(role);

        if (wanted.SizePx > ceiling)
        {
            face.ReducedTo = ceiling;
            wanted = wanted with { SizePx = ceiling };
        }

        // Only custom roles get a fallback built, since a stock face cannot fail to load.
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

        // Only while the decode effect is on, since it is the only thing that draws with this
        // handle. Built unconditionally it cost three faces' worth of atlas for every player with
        // the effect off.
        //
        // Default ranges on purpose. The Eorzean font is a Latin-only recreation of the in-game
        // alphabet, so asking it for kana and kanji would reserve thousands of glyphs it does not
        // contain and cost atlas space for nothing.
        face.Eorzean = this.config.DecodeEffectEnabled
            ? BuildFromFile(BundledEorzeanPath(), wanted.SizePx, glyphRanges: null)
            : null;
    }

    private IFontHandle BuildStock(FontChoice choice, float sizePx) =>
        choice == FontChoice.NotoSansCjk
            ? this.atlas.NewDelegateFontHandle(e => e.OnPreBuild(tk =>
                tk.AddDalamudAssetFont(
                    DalamudAsset.NotoSansCjkMedium,
                    new SafeFontConfig { SizePx = sizePx, GlyphRanges = NotificationGlyphRanges() })))
            : this.atlas.NewGameFontHandle(new GameFontStyle(ResolveGameFamily(choice), sizePx));

    private IFontHandle? BuildCustom(Face face, string path, float sizePx)
    {
        // The same ranges the stock face gets. Without them ImGui builds its default set, which is
        // Latin-1 only, and a player on the Japanese client who picks a custom font gets blanks for
        // every place name with nothing anywhere saying why.
        var handle = BuildFromFile(path, sizePx, NotificationGlyphRanges());

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

    private static ushort[]? CachedNotificationGlyphRanges;

    // Whether the notification faces have to carry kanji. A notification only ever draws place,
    // area and weather names, and those come out of the client's own sheets in the client's own
    // language, so nothing on an English, German or French client is written in kanji.
    //
    // The Fonts tab reads this too: it decides what size ceiling it can afford to offer.
    internal static bool NeedsCjkGlyphs =>
        Plugin.ClientState.ClientLanguage == ClientLanguage.Japanese;

    // Latin-1 and Latin Extended-A. Latin-1 alone covers the German and French place names, and
    // the extension is carried for the handful of ligatures and accents beyond it that cost
    // nothing to include.
    private static readonly ushort[] LatinGlyphRanges = [0x0020, 0x017F, 0];

    // Every glyph a notification face has to be able to draw. ImGui's "Japanese" set is a misnomer
    // for our purposes: it carries Latin-1 as well as kana and kanji.
    //
    // Which set is asked for is what decides the size ceiling the Fonts tab can offer, because
    // atlas cost is the glyph count times the square of the size, and the two sets are about 350
    // glyphs against 3,736. Measured against the bundled Noto with all three roles at their
    // ceiling: 16 MB of atlas texture for the Latin set, 352 MB for the Japanese one.
    private static unsafe ushort[] NotificationGlyphRanges()
    {
        if (CachedNotificationGlyphRanges != null)
            return CachedNotificationGlyphRanges;

        if (!NeedsCjkGlyphs)
            return CachedNotificationGlyphRanges = LatinGlyphRanges;

        var source = ImGui.GetIO().Fonts.GetGlyphRangesJapanese();

        var length = 0;
        while (source[length] != 0)
            length++;

        var ranges = new ushort[length + 1];
        for (var i = 0; i < length; i++)
            ranges[i] = source[i];

        return CachedNotificationGlyphRanges = ranges;
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
