using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Configuration;
using RegionsOfXIV.Services;

namespace RegionsOfXIV;

[Serializable]
// Every setting, stored flat rather than grouped into nested objects. Grouping would read
// better but would change the shape of the saved JSON and silently reset everyone, so the
// grouping lives in the config window instead.
//
// Adding a setting here is enough for it to be saved, to travel in presets and share codes, and
// to be covered by the round-trip tests -- see ConfigurationCopy.
public class Configuration : IPluginConfiguration, IGateSettings
{
    public const int CurrentVersion = 3;

    public int Version { get; set; } = CurrentVersion;

    public string? LastSeenVersion { get; set; }

    public bool ZoneNotificationEnabled { get; set; } = true;

    public bool AreaNotificationEnabled { get; set; } = true;

    public bool SubAreaNotificationEnabled { get; set; } = true;

    public bool IncludeParentTierAsHeader { get; set; } = true;

    public bool WeatherNotificationEnabled { get; set; } = false;

    public bool ShowWeatherIcon { get; set; } = true;

    public bool BannerNotificationEnabled { get; set; } = false;

    public bool HideNativeAreaText { get; set; } = true;

    public bool HideNativeLoadingTitle { get; set; } = true;

    public bool HideNativeBanner { get; set; } = true;

    public float VerticalPosition { get; set; } = 25f;

    public float HorizontalPosition { get; set; } = 50f;

    public float DisplayFontSize { get; set; } = 91f;

    public FontChoice DisplayFont { get; set; } = FontChoice.NotoSansCjk;

    public string DisplayFontPath { get; set; } = string.Empty;

    public float HeaderFontSize { get; set; } = 24f;

    public FontChoice HeaderFont { get; set; } = FontChoice.Axis;

    public string HeaderFontPath { get; set; } = string.Empty;

    public float WeatherFontSize { get; set; } = 24f;

    public FontChoice WeatherFont { get; set; } = FontChoice.Axis;

    public string WeatherFontPath { get; set; } = string.Empty;

    public float LetterSpacing { get; set; } = 0f;

    public bool UppercaseText { get; set; } = false;

    public bool UnderlineHeader { get; set; } = true;

    // Superseded by HeaderGap. Kept only so a config written before version 3 can be migrated,
    // and excluded from ConfigurationCopy so it no longer travels in presets.
    public bool OverlapHeader { get; set; } = true;

    public const float OverlappedHeaderGap = 1.1f;

    public const float SpacedHeaderGap = 1.6f;

    public float HeaderGap { get; set; } = OverlappedHeaderGap;

    public bool DecodeEffectEnabled { get; set; } = true;

    public MotionEffect Motion { get; set; } = MotionEffect.None;

    public ParticleEffect Particles { get; set; } = ParticleEffect.None;

    public float ParticleDensity { get; set; } = 1f;

    public Vector4 ParticleColor { get; set; } = new(1f, 0.72f, 0.35f, 1f);

    public Vector4 TextColor { get; set; } = new(0.875f, 0.761f, 0.584f, 1f);

    public Vector4 HeaderColor { get; set; } = new(0.698f, 0.627f, 0.569f, 1f);

    public Vector4 StrokeColor { get; set; } = new(0f, 0f, 0f, 0.8f);

    public bool SeparateLineColors { get; set; } = false;

    public Vector4 WeatherColor { get; set; } = new(0.698f, 0.627f, 0.569f, 1f);

    public Vector4 HeaderStrokeColor { get; set; } = new(0f, 0f, 0f, 0.8f);

    public Vector4 WeatherStrokeColor { get; set; } = new(0f, 0f, 0f, 0.8f);

    public float StrokeThickness { get; set; } = 1f;

    public bool ShadowEnabled { get; set; } = false;

    public Vector4 ShadowColor { get; set; } = new(0f, 0f, 0f, 0.65f);

    public float ShadowOffsetX { get; set; } = 2f;

    public float ShadowOffsetY { get; set; } = 2f;

    public float ShadowSoftness { get; set; } = 0f;

    public List<UserPreset> UserPresets { get; set; } = [];

    public TimeSpan FadeInDuration { get; set; } = TimeSpan.FromSeconds(0.9);

    public TimeSpan MotionDuration { get; set; } = TimeSpan.FromSeconds(1.1);

    public TimeSpan RevealDuration { get; set; } = TimeSpan.FromSeconds(1.3);

    public TimeSpan ShowDuration { get; set; } = TimeSpan.FromSeconds(4);

    public TimeSpan FadeOutDuration { get; set; } = TimeSpan.FromSeconds(2);

    public bool HideInCombat { get; set; } = false;

    public bool HideInDuty { get; set; } = false;

    public bool HideWhileTravellingFast { get; set; } = true;

    public FontSetting FontFor(FontRole role) => role switch
    {
        FontRole.Header => new FontSetting(HeaderFont, HeaderFontPath, HeaderFontSize),
        FontRole.Weather => new FontSetting(WeatherFont, WeatherFontPath, WeatherFontSize),
        _ => new FontSetting(DisplayFont, DisplayFontPath, DisplayFontSize),
    };

    public void SetFontFor(FontRole role, in FontSetting setting)
    {
        switch (role)
        {
            case FontRole.Header:
                (HeaderFont, HeaderFontPath, HeaderFontSize) = setting;
                break;

            case FontRole.Weather:
                (WeatherFont, WeatherFontPath, WeatherFontSize) = setting;
                break;

            default:
                (DisplayFont, DisplayFontPath, DisplayFontSize) = setting;
                break;
        }
    }

    // The one place that decides whether a line gets a header. Both the live announcement path
    // and the config-window preview go through here; when the preview had its own copy of this
    // rule, "Show header" quietly stopped working in the preview only.
    public string? HeaderFor(string? parent, string? text)
    {
        if (!IncludeParentTierAsHeader || parent is null)
            return null;

        return string.Equals(parent, text, StringComparison.OrdinalIgnoreCase) ? null : parent;
    }

    public bool UsesCustomFont =>
        DisplayFont == FontChoice.Custom
        || HeaderFont == FontChoice.Custom
        || WeatherFont == FontChoice.Custom;

    // The floor under every colour's alpha. Transparency is worth having -- a line can be pushed
    // back behind the others -- but a colour faded the whole way to nothing reads as a plugin that
    // has broken rather than a line that is hidden, and that is what the old unbounded alpha bar
    // kept producing by accident.
    public const float MinAlpha = 0.15f;

    // Enforced on load and on import as well as in the picker, because a config or a share code
    // written before the floor existed can still carry a zero. Raised to the floor rather than to
    // the colour's default, since a faint colour may well have been chosen deliberately.
    public bool RepairFaintColors()
    {
        var repaired = false;

        foreach (var property in ConfigurationCopy.Settings)
        {
            if (property.PropertyType != typeof(Vector4))
                continue;

            var colour = (Vector4)property.GetValue(this)!;
            if (colour.W >= MinAlpha)
                continue;

            property.SetValue(this, colour with { W = MinAlpha });
            repaired = true;

            Log.Information(
                $"{property.Name} was stored too faint to see, so it was raised to the minimum.");
        }

        return repaired;
    }

    // Runs once on load, before anything reads the config. Each step is guarded on the version
    // the setting was introduced in, so a config can jump several versions in one go. Steps must
    // stay in ascending order and must never assume an earlier step ran this session.
    public bool Migrate()
    {
        if (Version == CurrentVersion)
            return false;

        if (Version > CurrentVersion)
        {
            Log.Warning(
                $"The stored configuration is version {Version}, newer than this build understands " +
                $"({CurrentVersion}). Reading what applies and leaving the file as it is.");
            return false;
        }

        var from = Version;

        if (Version < 2)
            WeatherFontSize = HeaderFontSize;

        if (Version < 3)
            HeaderGap = OverlapHeader ? OverlappedHeaderGap : SpacedHeaderGap;

        Version = CurrentVersion;
        Log.Information($"Migrated the configuration from version {from} to {CurrentVersion}.");
        return true;
    }

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
