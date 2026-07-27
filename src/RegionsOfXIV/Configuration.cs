using System;
using System.Numerics;
using Dalamud.Configuration;

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

[Serializable]
public class Configuration : IPluginConfiguration
{
    // Bump when the shape changes, and migrate on load.
    public int Version { get; set; } = 1;

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

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
