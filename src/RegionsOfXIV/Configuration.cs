using System;
using System.Numerics;
using Dalamud.Configuration;
using RegionsOfXIV.Services;

namespace RegionsOfXIV;

// The game's fonts are bitmap atlases baked at fixed sizes rather than vector
// outlines, so any size above a family's largest native size is an upscale and
// visibly softens. Each game face therefore comes with a ceiling:
//
//   TrumpGothic  ~91 px   the narrow title face. Latin only.
//   Jupiter      ~61 px   the serif face. Latin only.
//   Axis          48 px   the general UI face, and the only game family carrying
//                         Japanese glyphs. Lowest ceiling of the three.
//   NotoSansCjk    none   shipped with Dalamud, and vector rather than bitmap, so
//                         it is crisp at any size and covers every language.
//
// Appended in that order deliberately: the numeric values are what land in the
// saved config, so new faces go on the end. NotoSansCjk is last for that reason
// alone — it is the default for new configs despite being added most recently, and
// renumbering to put it first would silently reassign every existing user's choice
// (a stored 0 would stop meaning TrumpGothic).
public enum DisplayFontChoice
{
    TrumpGothic,
    Jupiter,
    Axis,
    NotoSansCjk,
}

// IGateSettings is satisfied by the properties below as they already stand; it
// names the subset NotificationGate reads so the gate can be built without this
// class. See Services/IGateSettings.cs.
[Serializable]
public class Configuration : IPluginConfiguration, IGateSettings
{
    // The shape this build understands.
    //
    // Adding a property does not need a bump: an absent field leaves the property
    // at the default its initializer gave it, which is the right answer for a new
    // setting. It is renames, retypes and changes of *meaning* that need one —
    // those are the cases where a file reads cleanly but says the wrong thing.
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    // Which tiers to announce. All three on: the plugin now hides the game's own
    // zone title rather than sitting alongside it, so leaving the zone tier off
    // would mean a zone change announced by nobody.
    public bool ZoneNotificationEnabled { get; set; } = true;

    public bool AreaNotificationEnabled { get; set; } = true;

    public bool SubAreaNotificationEnabled { get; set; } = true;

    public bool IncludeParentTierAsHeader { get; set; } = true;

    // The game's own "_AreaText" flash covers the same ground as this plugin, and
    // renders underneath it. On by default, since showing both is never wanted.
    public bool HideNativeAreaText { get; set; } = true;

    // Suppresses the loading-screen zone title, which the game splits across two
    // addons: "_LocationTitle" and "_LocationTitleShort".
    public bool HideNativeLoadingTitle { get; set; } = true;

    // Layout and style.
    // VerticalPosition is a percentage of viewport height.
    public float VerticalPosition { get; set; } = 25f;

    // Large enough to read as a title rather than as a label. No ceiling applies
    // at the default font, which is vector; and 91 px is exactly TrumpGothic's
    // ceiling, so switching to the FFXIV display face keeps it sharp too. Jupiter
    // and Axis run out well below this and will say so.
    public float DisplayFontSize { get; set; } = 91f;

    public float HeaderFontSize { get; set; } = 24f;

    // Noto by default: it is the only choice that needs no caveat, being sharp at
    // any size and carrying glyphs for every language the client can display. The
    // three game faces look more like FFXIV and are a click away.
    public DisplayFontChoice DisplayFont { get; set; } = DisplayFontChoice.NotoSansCjk;

    public bool UnderlineHeader { get; set; } = true;

    public bool OverlapHeader { get; set; } = true;

    // Eorzean -> Latin decode during the reveal. Silently inert when no font is
    // bundled, or when the text falls outside the font's Latin coverage.
    public bool DecodeEffectEnabled { get; set; } = true;

    // Vector4 rather than a packed uint: it is what ImGui's colour pickers take,
    // and it stays legible if anyone opens the config file.
    public Vector4 TextColor { get; set; } = new(0.875f, 0.761f, 0.584f, 1f);

    public Vector4 HeaderColor { get; set; } = new(0.698f, 0.627f, 0.569f, 1f);

    public Vector4 StrokeColor { get; set; } = new(0f, 0f, 0f, 0.8f);

    // Durations.
    public TimeSpan FadeInDuration { get; set; } = TimeSpan.FromSeconds(0.9);

    public TimeSpan RevealDuration { get; set; } = TimeSpan.FromSeconds(0.9);

    public TimeSpan ShowDuration { get; set; } = TimeSpan.FromSeconds(4);

    public TimeSpan FadeOutDuration { get; set; } = TimeSpan.FromSeconds(2);

    // Suppression, both off.
    //
    // These made sense while the plugin drew on top of the game's own notices —
    // silencing it still left the native text. Now that it replaces that text,
    // suppressing here means no location feedback at all, which is worse than the
    // distraction it was guarding against. Combat and duties are also exactly when
    // sub-areas change most.
    //
    // Cutscenes, PvP and gpose remain suppressed unconditionally; those are not
    // negotiable and are handled in NotificationGate rather than here.
    public bool HideInCombat { get; set; } = false;

    public bool HideInDuty { get; set; } = false;

    // Sub-areas only, and only above a speed no ground travel reaches — see
    // NotificationGate.TravellingSpeed. On by default: flying across a zone
    // otherwise announces a string of places the player passed over rather than
    // visited, which is the one case where the announcements become noise.
    public bool HideWhileTravellingFast { get; set; } = true;

    // Carries a stored configuration forward to CurrentVersion, reporting whether
    // anything actually moved — i.e. whether the result is worth writing back.
    //
    // Migration steps go here as a ladder, oldest first, each covering one hop:
    //
    //     if (Version < 2) { NewSetting = OldSetting ? 1f : 0f; Version = 2; }
    //     if (Version < 3) { ...; Version = 3; }
    //
    // A ladder rather than a switch, so a file several versions old runs every
    // step between it and the present, in order, and each step only has to
    // describe its own hop.
    //
    // There are no steps yet: version 1 is the first shape to ship. What this does
    // do today is notice the one case that is already reachable — a config written
    // by a newer build than this one.
    public bool Migrate()
    {
        if (Version == CurrentVersion)
            return false;

        // Downgrade. Nothing here can know what a later shape meant, and rewriting
        // the file would discard whatever that build stored without being able to
        // put it back if the user upgrades again. Deserialization has already
        // filled anything unreadable with defaults, so run with what we got and
        // leave the file alone.
        if (Version > CurrentVersion)
        {
            Plugin.Log.Warning(
                $"The stored configuration is version {Version}, newer than this build understands " +
                $"({CurrentVersion}). Reading what applies and leaving the file as it is.");
            return false;
        }

        var from = Version;

        // (no steps yet)

        Version = CurrentVersion;
        Plugin.Log.Information($"Migrated the configuration from version {from} to {CurrentVersion}.");
        return true;
    }

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
