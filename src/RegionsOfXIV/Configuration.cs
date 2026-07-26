using System;
using System.Numerics;
using Dalamud.Configuration;

namespace RegionsOfXIV;

// Of the game's font families only Axis carries Japanese glyphs; TrumpGothic and
// Jupiter are Latin-only, so Auto has to fall back for a JP client.
//
// The game's fonts are bitmap atlases baked at fixed sizes, not vector outlines,
// so any size above a family's largest native size is an upscale and softens.
// Dalamud is the escape hatch: a vector face that stays sharp at any size.
public enum DisplayFontChoice
{
    Auto,
    TrumpGothic,
    Jupiter,
    Axis,
    Dalamud,
}

[Serializable]
public class Configuration : IPluginConfiguration
{
    // Bump when the shape changes, and migrate on load.
    public int Version { get; set; } = 1;

    // Which tiers to announce.
    // Zone is off by default: FFXIV already announces zone changes on screen, so
    // enabling it is for people who prefer this plugin's styling to the native
    // title. Sub-area is the headline feature — the game never announces those.
    public bool ZoneNotificationEnabled { get; set; } = false;

    public bool AreaNotificationEnabled { get; set; } = true;

    public bool SubAreaNotificationEnabled { get; set; } = true;

    public bool IncludeParentTierAsHeader { get; set; } = true;

    // The game's own "_AreaText" flash covers the same ground as this plugin, and
    // renders underneath it. On by default, since showing both is never wanted.
    public bool HideNativeAreaText { get; set; } = true;

    // Suppresses the loading-screen title ("_Image") during zone transitions.
    public bool HideNativeLoadingTitle { get; set; } = true;

    // Layout and style.
    // VerticalPosition is a percentage of viewport height.
    public float VerticalPosition { get; set; } = 25f;

    public float DisplayFontSize { get; set; } = 76f;

    public float HeaderFontSize { get; set; } = 24f;

    public DisplayFontChoice DisplayFont { get; set; } = DisplayFontChoice.Auto;

    public bool UnderlineHeader { get; set; } = true;

    public bool OverlapHeader { get; set; } = false;

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

    // Suppression.
    // HideInDuty defaults on, unlike the GW2 original: FFXIV instance content has
    // sub-areas and players are busy.
    public bool HideInCombat { get; set; } = true;

    public bool HideInDuty { get; set; } = true;

    // Sound. Off by default.
    public bool RevealSoundEnabled { get; set; } = false;

    public uint RevealSoundEffectId { get; set; } = 1;

    public bool VanishSoundEnabled { get; set; } = false;

    public uint VanishSoundEffectId { get; set; } = 2;

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
