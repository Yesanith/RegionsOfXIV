using System;
using System.IO;
using System.Linq;
using Dalamud;
using Dalamud.Bindings.ImGui;
using Dalamud.Game;

namespace RegionsOfXIV.Services;

// What a font choice, a role and this client can do, asked without building anything.
//
// Separate from FontService because the answers do not depend on a built atlas and most of the
// callers have no instance to ask: the Fonts tab needs the size ceilings to bound its sliders and
// the file check to vet a path, and the Presets tab needs the file check before it will warn
// about a share code. Reaching those through the service meant a class that owns font handles
// also answered questions that have nothing to do with owning them.
internal static class FontLimits
{
    private const long MaxCustomFontBytes = 64L * 1024 * 1024;

    private static readonly string[] CustomFontExtensions = [".ttf", ".otf", ".ttc"];

    // Where the game's own faces stop having real glyphs. Past this the client scales a bitmap and
    // the letters go soft, so the Fonts tab warns rather than pretending the size is available.
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
    // The Fonts tab offers this as the slider maximum, and FontService.Build holds to it as well,
    // because a config can carry a larger number from a time when the client was not Japanese.
    // Honouring that number would mean a 352 MB atlas where the same settings cost 16 MB in
    // English.
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

    // The half of Latin Extended-A that ImGui's Japanese set leaves out. Turkish needs it for
    // dotted I, S-cedilla and G-breve, and a player on the Japanese client who picks Turkish
    // banner wording would otherwise get a question mark for each of them.
    private static readonly ushort[] LatinExtendedRange = [0x0100, 0x017F];

    // The same block on its own, terminated, for the bundled face that fills it in. Asking for the
    // whole block rather than only the gaps is deliberate: ImGui keeps the first font to offer a
    // glyph, so what the CJK face already has still wins, and this does not have to be revisited
    // if Dalamud ever ships a different asset.
    public static readonly ushort[] LatinExtendedGlyphRanges = [0x0100, 0x017F, 0];

    private static ushort[]? CachedNotificationGlyphRanges;

    // Every glyph a notification face has to be able to draw. ImGui's "Japanese" set is a misnomer
    // for our purposes: it carries Latin-1 as well as kana and kanji, and only Latin-1, which is
    // why Latin Extended-A is added to it here. The Latin set already reaches that far.
    //
    // Which set is asked for is what decides the size ceiling above, because atlas cost is the
    // glyph count times the square of the size, and the two sets are about 350 glyphs against
    // 3,736. Measured against the bundled Noto with all three roles at their ceiling: 16 MB of
    // atlas texture for the Latin set, 352 MB for the Japanese one.
    public static unsafe ushort[] NotificationGlyphRanges()
    {
        if (CachedNotificationGlyphRanges != null)
            return CachedNotificationGlyphRanges;

        if (!NeedsCjkGlyphs)
            return CachedNotificationGlyphRanges = LatinGlyphRanges;

        var source = ImGui.GetIO().Fonts.GetGlyphRangesJapanese();

        var length = 0;
        while (source[length] != 0)
            length++;

        // The extension goes in front, which the range list allows: ImGui reads pairs until the
        // terminating zero and does not require them in order.
        var extra = LatinExtendedRange.Length;
        var ranges = new ushort[extra + length + 1];

        LatinExtendedRange.CopyTo(ranges, 0);

        for (var i = 0; i < length; i++)
            ranges[extra + i] = source[i];

        return CachedNotificationGlyphRanges = ranges;
    }
}
