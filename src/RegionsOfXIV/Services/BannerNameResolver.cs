using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.Game;
using Lumina.Excel.Sheets;

namespace RegionsOfXIV.Services;

// A banner's wording exists only as pixels inside its artwork, so there is no string to read.
// Two sources are crossed to recover it: GroupPoseStamp names a dozen or so of the same icons for
// the gpose stamp picker and is localised by the game, and ScreenImage says which of those icons
// are actually used as banners. Anything left over comes from a hand-read table in BannerNames,
// which exists per language.
//
// ScreenImage is also where the ids themselves come from, which is worth stating plainly because
// it is easy to assume otherwise: the sheet lists far more banners than anything has a name for,
// so the gap is names, not discovery.
//
// Its own file rather than sharing NameResolvers.cs with the place and weather resolvers. Those
// two are a sheet lookup each; this one carries a chosen language, a cache keyed on it, a casing
// culture and the crossing of two sheets against a hand-read table.
internal static class BannerNameResolver
{
    private static (Dictionary<uint, string> Named, HashSet<uint> Ids)? loaded;

    private static string? language;

    // Which language the wording is read from, or null to follow the client. Set from the config.
    //
    // Assigning a different value drops the cache, because the table decides both the names and
    // the id set: the named ids are folded into KnownIds, so a language change moves both and the
    // preview window would otherwise keep listing the old set until a reload.
    public static string? Language
    {
        get => language;

        set
        {
            if (string.Equals(language, value, StringComparison.OrdinalIgnoreCase))
                return;

            language = value;
            loaded = null;
        }
    }

    private static (Dictionary<uint, string> Named, HashSet<uint> Ids) Data => loaded ??= Build();

    public static IReadOnlyDictionary<uint, string> All => Data.Named;

    // Every banner id the plugin knows of, named or not. The unnamed ones are the working set for
    // /regions preview; they are useless to Resolve and deliberately invisible to it.
    public static IReadOnlySet<uint> KnownIds => Data.Ids;

    public static string? Resolve(uint iconId) =>
        iconId != 0 && All.TryGetValue(iconId, out var name) ? name : null;

    // The culture the wording should be cased with. Not the player's locale, which is what the
    // invariant call this replaced was guarding against, but the language the wording itself is
    // written in, which is a different question and follows the banner language rather than the
    // client.
    //
    // It matters because banners are always drawn uppercase and Turkish cases differently from
    // everything else: "i" upcases to a dotted capital and "ı" to a plain one, so invariant rules
    // drop the dot from every "i" and leave every "ı" in lower case.
    //
    // Only an explicit choice can change the rules. Following the client the wording is in the
    // client's own language, and FFXIV ships in English, German, French and Japanese, none of
    // which case differently from invariant. Turkish can only arrive by being asked for.
    //
    // That is also what keeps this out of Plugin.ClientState, so pushing a banner stays something
    // the tests can do without a running client.
    public static CultureInfo Casing =>
        language is null ? CultureInfo.InvariantCulture : CultureFor(language);

    private static (Dictionary<uint, string> Named, HashSet<uint> Ids) Build()
    {
        var stamps = new Dictionary<uint, string>();

        foreach (var stamp in Plugin.DataManager.GetExcelSheet<GroupPoseStamp>())
        {
            if (stamp.StampIcon <= 0)
                continue;

            var name = stamp.Name.ToString();

            if (!string.IsNullOrWhiteSpace(name))
                stamps.TryAdd((uint)stamp.StampIcon, name.Trim());
        }

        var ids = new HashSet<uint>();
        var found = new Dictionary<uint, string>();

        foreach (var image in Plugin.DataManager.GetExcelSheet<ScreenImage>())
        {
            // Lang marks an image that ships once per client language, which is the same thing as
            // saying its wording is painted in. The rest of the sheet -- a quarter of it -- is
            // 1280x720 cutscene stills with no text in them at all, and listing those as banners
            // waiting to be named would bury the ones that are.
            if (image.Image == 0 || !image.Lang)
                continue;

            if (!ids.Add(image.Image))
                continue;

            if (stamps.TryGetValue(image.Image, out var name))
                found[image.Image] = name;
        }

        var fromScreenImage = ids.Count;
        var fromStamps = found.Count;

        // The chosen table wins over the stamps rather than filling in behind them. Following the
        // client the two agree, so this changes nothing; but a player who has picked a language
        // their client is not in would otherwise get those dozen ids in the client's language and
        // everything else in the chosen one, which reads as a bug rather than as a mixture.
        //
        // Stamps still supply the ids the table has no wording for, in whatever language the
        // client is. That is a name in the wrong language rather than no name at all, and losing
        // them would cost the players who chose nothing.
        if (TableFor(language) is { } table)
        {
            foreach (var (icon, name) in table)
            {
                found[icon] = name;

                // A handful of banners that have been walked into in-game are not in ScreenImage
                // at all, so the sheet is a floor rather than the whole truth. Folding the named
                // ones back in keeps the invariant the preview window relies on: every id that
                // has a name is an id you can fire.
                ids.Add(icon);
            }
        }

        Log.Debug(
            $"Banners: {ids.Count} ids known ({fromScreenImage} from ScreenImage), {found.Count} of them named "
            + $"-- {fromStamps} from stamps, in {language ?? ClientCode()}.");

        return (found, ids);
    }

    private static CultureInfo CultureFor(string code)
    {
        try
        {
            return CultureInfo.GetCultureInfo(code);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }

    // No table for the language means no wording, so every id resolves to null and BannerWatcher
    // leaves the game's own banner alone. That is the same path an unnamed id already takes, so
    // nothing downstream needs to know a language was involved.
    private static IReadOnlyDictionary<uint, string>? TableFor(string? code) =>
        BannerNames.ByLanguage.GetValueOrDefault(code ?? ClientCode());

    private static string ClientCode() => Plugin.ClientState.ClientLanguage switch
    {
        ClientLanguage.English => "en",
        ClientLanguage.German => "de",
        ClientLanguage.French => "fr",
        ClientLanguage.Japanese => "ja",
        _ => string.Empty,
    };
}
