using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Configuration;
using RegionsOfXIV.Services;

namespace RegionsOfXIV;

[Serializable]
public class Configuration : IPluginConfiguration, IGateSettings
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    public string? LastSeenVersion { get; set; }

    public bool ZoneNotificationEnabled { get; set; } = true;

    public bool AreaNotificationEnabled { get; set; } = true;

    public bool SubAreaNotificationEnabled { get; set; } = true;

    public bool IncludeParentTierAsHeader { get; set; } = true;

    public bool WeatherNotificationEnabled { get; set; } = false;

    public bool ShowWeatherIcon { get; set; } = true;

    public bool HideNativeAreaText { get; set; } = true;

    public bool HideNativeLoadingTitle { get; set; } = true;

    public float VerticalPosition { get; set; } = 25f;

    public float HorizontalPosition { get; set; } = 50f;

    public float DisplayFontSize { get; set; } = 91f;

    public float HeaderFontSize { get; set; } = 24f;

    public DisplayFontChoice DisplayFont { get; set; } = DisplayFontChoice.NotoSansCjk;

    public float LetterSpacing { get; set; } = 0f;

    public bool UppercaseText { get; set; } = false;

    public bool UnderlineHeader { get; set; } = true;

    public bool OverlapHeader { get; set; } = true;

    public bool DecodeEffectEnabled { get; set; } = true;

    public MotionEffect Motion { get; set; } = MotionEffect.None;

    public ParticleEffect Particles { get; set; } = ParticleEffect.None;

    public float ParticleDensity { get; set; } = 1f;

    public Vector4 ParticleColor { get; set; } = new(1f, 0.72f, 0.35f, 1f);

    public Vector4 TextColor { get; set; } = new(0.875f, 0.761f, 0.584f, 1f);

    public Vector4 HeaderColor { get; set; } = new(0.698f, 0.627f, 0.569f, 1f);

    public Vector4 StrokeColor { get; set; } = new(0f, 0f, 0f, 0.8f);

    public float StrokeThickness { get; set; } = 1f;

    public List<UserPreset> UserPresets { get; set; } = [];

    public TimeSpan FadeInDuration { get; set; } = TimeSpan.FromSeconds(0.9);

    public TimeSpan MotionDuration { get; set; } = TimeSpan.FromSeconds(1.1);

    public TimeSpan RevealDuration { get; set; } = TimeSpan.FromSeconds(1.3);

    public TimeSpan ShowDuration { get; set; } = TimeSpan.FromSeconds(4);

    public TimeSpan FadeOutDuration { get; set; } = TimeSpan.FromSeconds(2);

    public bool HideInCombat { get; set; } = false;

    public bool HideInDuty { get; set; } = false;

    public bool HideWhileTravellingFast { get; set; } = true;

    public bool Migrate()
    {
        if (Version == CurrentVersion)
            return false;

        if (Version > CurrentVersion)
        {
            Plugin.Log.Warning(
                $"The stored configuration is version {Version}, newer than this build understands " +
                $"({CurrentVersion}). Reading what applies and leaving the file as it is.");
            return false;
        }

        var from = Version;

        Version = CurrentVersion;
        Plugin.Log.Information($"Migrated the configuration from version {from} to {CurrentVersion}.");
        return true;
    }

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
