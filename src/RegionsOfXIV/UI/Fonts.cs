using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Dalamud;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using RegionsOfXIV.Services;

namespace RegionsOfXIV.UI;

// The faces the plugin's own windows draw with, in tiers that are ratios of the size chosen in
// Dalamud settings, so the window follows that setting instead of pinning pixels of its own.
//
// Latin comes from the bundled Noto Sans Medium subset, which carries Basic Latin through Latin
// Extended Additional, so Turkish, Polish, Czech, Romanian and Vietnamese draw in one typeface.
// Everything else is merged in from Dalamud's Noto Sans CJK: Greek, Cyrillic, kana, the kanji
// ImGui's Japanese set names, and whatever else the bundled locale files use.
//
// These are not the notification fonts. FontService owns those.
internal static class Fonts
{
    private const float CaptionScale = 1.0f;
    private const float BodyScale = 1.125f;
    private const float HeadlineScale = 1.25f;
    private const float TitleScale = 1.625f;
    private const float IconScale = BodyScale;
    private const float IconLargeScale = 1.625f;
    private const float IconDisplayScale = 2.25f;

    private const string LatinFontFile = "NotoSans-Medium-Latin.ttf";
    private const int CodepointCount = char.MaxValue + 1;

    // Turkish lowercase s-cedilla from the Latin face and hiragana "a" from the merge: if either
    // is missing after a build, the file did not load or the merge did not land.
    private const char LatinProbe = 'ş';
    private const char MergeProbe = 'あ';

    private static readonly ushort[] LatinBlocks = [0x0020, 0x024F, 0x0300, 0x036F, 0x1E00, 0x1EFF, 0x2000, 0x206F, 0x20A0, 0x20CF, 0];

    private static readonly ushort[] MergedBlocks = [0x0370, 0x03FF, 0x0400, 0x04FF, 0x2190, 0x21FF, 0x2200, 0x22FF, 0x3000, 0x30FF, 0xFF00, 0xFFEF, 0];

    private static readonly NoOpScope NoOp = new();

    private static IUiBuilder? Builder;
    private static IFontAtlas? Atlas;
    private static byte[]? LatinFont;
    private static ushort[] MergeRanges = [0];
    private static float UnitPx = UiBuilder.DefaultFontSizePx;

    private static IFontHandle? Body;
    private static IFontHandle? Title;
    private static IFontHandle? Headline;
    private static IFontHandle? Caption;
    private static IFontHandle? Icon;
    private static IFontHandle? IconLarge;
    private static IFontHandle? IconDisplay;

    public static void Initialize(IUiBuilder uiBuilder, string pluginDirectory)
    {
        Builder = uiBuilder;
        Atlas = uiBuilder.FontAtlas;
        LatinFont = LoadLatinFont(Path.Combine(pluginDirectory, "Fonts", LatinFontFile));
        MergeRanges = BuildMergeRanges();
        UnitPx = uiBuilder.DefaultFontSpec.SizePx;

        Body = TextHandle(BodyScale);
        Title = TextHandle(TitleScale);
        Headline = TextHandle(HeadlineScale);
        Caption = TextHandle(CaptionScale);
        Icon = IconHandle(IconScale);
        IconLarge = IconHandle(IconLargeScale);
        IconDisplay = IconHandle(IconDisplayScale);

        Body.ImFontChanged += CheckTheBuildLanded;
        uiBuilder.DefaultFontChanged += Rebuild;

        if (Atlas.AutoRebuildMode == FontAtlasAutoRebuildMode.Disable)
        {
            _ = Atlas.BuildFontsAsync();
        }
    }

    public static void Dispose()
    {
        if (Builder is not null)
        {
            Builder.DefaultFontChanged -= Rebuild;
        }

        if (Body is not null)
        {
            Body.ImFontChanged -= CheckTheBuildLanded;
        }

        Body?.Dispose();
        Title?.Dispose();
        Headline?.Dispose();
        Caption?.Dispose();
        Icon?.Dispose();
        IconLarge?.Dispose();
        IconDisplay?.Dispose();
        Body = Title = Headline = Caption = Icon = IconLarge = IconDisplay = null;
        Builder = null;
        Atlas = null;
        LatinFont = null;
    }

    public static IDisposable PushBody() => Body?.Push() ?? NoOp;

    public static IDisposable PushTitle() => Title?.Push() ?? NoOp;

    public static IDisposable PushHeadline() => Headline?.Push() ?? NoOp;

    public static IDisposable PushCaption() => Caption?.Push() ?? NoOp;

    public static IDisposable PushIcon() => Icon?.Push() ?? ImRaii.PushFont(UiBuilder.IconFont);

    public static IDisposable PushIconLarge() => IconLarge?.Push() ?? ImRaii.PushFont(UiBuilder.IconFont);

    public static IDisposable PushIconDisplay() => IconDisplay?.Push() ?? ImRaii.PushFont(UiBuilder.IconFont);

    // The smallest tier that still covers the target, so a glyph fitted to a shape only ever shrinks.
    public static IDisposable PushIconFor(float targetHeight)
    {
        var unit = UnitPx * ImGuiHelpers.GlobalScale;
        if (targetHeight > unit * IconLargeScale)
        {
            return PushIconDisplay();
        }

        if (targetHeight > unit * IconScale)
        {
            return PushIconLarge();
        }

        return PushIcon();
    }

    private static byte[]? LoadLatinFont(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                return File.ReadAllBytes(path);
            }

            Log.Warning($"The window font is missing at '{path}'; drawing with the Dalamud default instead.");
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Could not read the window font; drawing with the Dalamud default instead.");
        }

        return null;
    }

    private static IFontHandle TextHandle(float scale)
        => Atlas!.NewDelegateFontHandle(e => e.OnPreBuild(tk =>
        {
            var sizePx = UnitPx * scale;
            if (LatinFont is null)
            {
                tk.AddDalamudDefaultFont(sizePx, null);
                return;
            }

            var primary = tk.AddFontFromMemory(LatinFont, new SafeFontConfig { SizePx = sizePx, GlyphRanges = LatinBlocks }, LatinFontFile);
            tk.Font = primary;
            tk.AddDalamudAssetFont(DalamudAsset.NotoSansCjkRegular, new SafeFontConfig
            {
                SizePx = sizePx,
                GlyphRanges = MergeRanges,
                MergeFont = primary,
            });
        }));

    private static IFontHandle IconHandle(float scale)
        => Atlas!.NewDelegateFontHandle(e => e.OnPreBuild(tk => tk.AddFontAwesomeIconFont(new SafeFontConfig { SizePx = UnitPx * scale })));

    private static void Rebuild()
    {
        UnitPx = Builder?.DefaultFontSpec.SizePx ?? UiBuilder.DefaultFontSizePx;
        if (Atlas is not null)
        {
            _ = Atlas.BuildFontsAsync();
        }
    }

    private static unsafe void CheckTheBuildLanded(IFontHandle sender, ILockedImFont locked)
    {
        if (LatinFont is null)
        {
            return;
        }

        if (locked.ImFont.FindGlyphNoFallback(LatinProbe) is null)
        {
            Log.Warning($"The window font did not gain its extended Latin glyphs: U+{(int)LatinProbe:X4} is missing after the build.");
        }

        if (locked.ImFont.FindGlyphNoFallback(MergeProbe) is null)
        {
            Log.Warning($"The window font did not gain its Japanese glyphs: U+{(int)MergeProbe:X4} is missing after the build.");
        }
    }

    // Everything the window may have to draw that the Latin face does not carry. Fixed when the
    // atlas is built, so every bundled language is included at once rather than the active one,
    // and a language change needs no rebuild.
    private static unsafe ushort[] BuildMergeRanges()
    {
        var present = new bool[CodepointCount];
        MarkRanges(present, MergedBlocks);

        var japanese = ImGui.GetIO().Fonts.GetGlyphRangesJapanese();
        for (var index = 0; japanese[index] != 0; index += 2)
        {
            for (int codepoint = japanese[index]; codepoint <= japanese[index + 1]; codepoint++)
            {
                present[codepoint] = true;
            }
        }

        foreach (var code in Loc.Shipped)
        {
            MarkLocale(present, code);
            MarkText(present, NativeName(code));
        }

        foreach (var code in BannerNames.ByLanguage.Keys)
        {
            MarkText(present, NativeName(code));
        }

        return ToRanges(present);
    }

    private static void MarkLocale(bool[] present, string code)
    {
        try
        {
            using var stream = typeof(Loc).Assembly.GetManifestResourceStream($"RegionsOfXIV.Localization.{code}.json");
            if (stream is null)
            {
                return;
            }

            foreach (var text in Loc.Parse(stream).Values)
            {
                MarkText(present, text);
            }
        }
        catch (Exception exception)
        {
            Log.Warning(exception, $"Could not read the {code} strings while sizing the window font.");
        }
    }

    private static string NativeName(string code)
    {
        try
        {
            return CultureInfo.GetCultureInfo(code).NativeName;
        }
        catch (CultureNotFoundException)
        {
            return code;
        }
    }

    private static void MarkText(bool[] present, string text)
    {
        for (var index = 0; index < text.Length; index++)
        {
            var codepoint = text[index];
            if (!char.IsSurrogate(codepoint))
            {
                present[codepoint] = true;
            }
        }
    }

    private static void MarkRanges(bool[] present, ushort[] ranges)
    {
        for (var index = 0; index + 1 < ranges.Length && ranges[index] != 0; index += 2)
        {
            for (int codepoint = ranges[index]; codepoint <= ranges[index + 1]; codepoint++)
            {
                present[codepoint] = true;
            }
        }
    }

    private static ushort[] ToRanges(bool[] present)
    {
        var ranges = new List<ushort>();
        var runStart = -1;
        for (var codepoint = 1; codepoint < CodepointCount; codepoint++)
        {
            if (present[codepoint])
            {
                if (runStart < 0)
                {
                    runStart = codepoint;
                }

                continue;
            }

            if (runStart < 0)
            {
                continue;
            }

            ranges.Add((ushort)runStart);
            ranges.Add((ushort)(codepoint - 1));
            runStart = -1;
        }

        if (runStart >= 0)
        {
            ranges.Add((ushort)runStart);
            ranges.Add(char.MaxValue);
        }

        ranges.Add(0);
        return [.. ranges];
    }

    private sealed class NoOpScope : IDisposable
    {
        public void Dispose() { }
    }
}
