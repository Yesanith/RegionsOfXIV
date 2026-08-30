using System;
using System.Globalization;
using Dalamud.Interface.ManagedFontAtlas;

namespace RegionsOfXIV.UI;

// The font the plugin's own windows draw with: Dalamud's default face, with the Windows interface
// font merged in behind it for the extended Latin blocks.
//
// Dalamud's default is the game's AXIS face, which was measured to carry eight characters of Latin
// Extended-A and nothing else from it. A Turkish, Polish, Czech or Romanian translation therefore
// drew blanks where most of its diacritics fell, which is what kept those languages out.
//
// The merge only fills in code points the base face does not already have, because the first font
// to claim one wins. Basic Latin still comes from AXIS, so an English window is untouched. The
// cost of that is a Turkish word mixing two typefaces, AXIS for its plain letters and the merged
// face for its diacritics, which is a good deal better than boxes.
//
// This is not the notification font. FontService owns those, they draw place and weather names out
// of game data, and no FFXIV client is Turkish, so none of this applies to them.
internal sealed class WindowFont : IDisposable
{
    // Latin Extended-A and B, then Latin Extended Additional for Vietnamese. Deliberately not
    // Hangul, Thai, Hebrew, Arabic or the CJK sets: they are large and no locale file wants them.
    private static readonly ushort[] MergedRanges = [0x0100, 0x024F, 0x1E00, 0x1EFF, 0];

    // Dalamud maps a culture to a Windows font family, and every Latin entry in its table maps to
    // Segoe UI, so one merge covers Turkish, Polish, Czech, Romanian and Vietnamese together.
    //
    // The region has to be spelled out. That table is matched case-sensitively against "tr-TR",
    // and a CultureInfo of "tr" alone probes "tr" and then "tr-tr", matches neither, and merges
    // nothing at all without raising anything.
    private static readonly CultureInfo MergeSource = new("tr-TR");

    // Turkish lowercase s-cedilla, U+015F. Sits in the middle of the merged range, so if the merge
    // landed this is there, and if it did not this is the first thing a Turkish reader loses.
    private const char MergeProbe = 'ş';

    private readonly IFontAtlas atlas;

    private readonly IFontHandle handle;

    public WindowFont()
    {
        this.atlas = Plugin.PluginInterface.UiBuilder.FontAtlas;

        // Built once and never rebuilt by hand. Every plugin-private atlas subscribes to Dalamud's
        // own AfterBuildFonts, so a change of interface scale or of the default font rebuilds this
        // atlas too and runs the delegate below again. Both values it reads are therefore read
        // inside it rather than captured, so a rebuild picks up the new ones.
        this.handle = this.atlas.NewDelegateFontHandle(e => e.OnPreBuild(tk =>
        {
            // A negative size means the default, so this follows whatever the player has set for
            // Dalamud's interface instead of pinning a number of its own.
            tk.AddDalamudDefaultFont(-1, null);

            // AddDalamudDefaultFont sets the toolkit's font to what it built, and this merges into
            // that when MergeFont is left unset. The size has to be stated: a merged face is built
            // at whatever it is given rather than inheriting from the font it joins.
            tk.AttachWindowsDefaultFont(
                MergeSource,
                new SafeFontConfig
                {
                    SizePx = Plugin.PluginInterface.UiBuilder.FontDefaultSizePx,
                    GlyphRanges = MergedRanges,
                });
        }));

        this.handle.ImFontChanged += CheckTheMergeLanded;
    }

    // AttachWindowsDefaultFont returns without a word when it cannot resolve the family, and the
    // culture tag it matches on is case-sensitive, so a merge that quietly did nothing looks
    // exactly like a merge that was never written. Nobody would find out except a Turkish reader
    // seeing blanks, so the built font is asked directly.
    private static unsafe void CheckTheMergeLanded(IFontHandle sender, ILockedImFont locked)
    {
        if (locked.ImFont.FindGlyphNoFallback(MergeProbe) is not null)
            return;

        Log.Warning(
            $"The window font did not gain the extended Latin glyphs from {MergeSource.Name}: "
            + $"U+{(int)MergeProbe:X4} is missing after the build. Turkish, Polish, Czech and "
            + "Romanian will draw blanks in this window.");
    }

    // Paired with Pop by the window's PreDraw and PostDraw, which run either side of the ImGui
    // window itself, so the title bar is drawn with it as well as the body. The returned handle is
    // what the caller pops, so a rebuild between the two cannot pop something else.
    public IDisposable Push() => this.handle.Push();

    public void Dispose()
    {
        this.handle.ImFontChanged -= CheckTheMergeLanded;
        this.handle.Dispose();
    }
}
