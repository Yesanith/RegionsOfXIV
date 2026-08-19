using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Configuration;
using RegionsOfXIV.Services;

namespace RegionsOfXIV;

// The stored settings. The enums they choose between live in
// Configuration.Options.cs, along with the append-only rule that governs them.
//
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

    // The plugin release whose changelog this player has already been shown, as
    // "0.2.2.0". Null on a config written before the changelog window existed.
    //
    // Nothing to do with Version above, which is the shape of this file. This one
    // tracks the plugin's own release number, and is not a preference — see
    // ConfigurationCopy, which excludes it from presets and share codes so that
    // importing somebody else's look cannot make the changelog reappear or vanish.
    public string? LastSeenVersion { get; set; }

    // --- what gets announced -----------------------------------------------

    // All three on: the plugin hides the game's own zone title rather than
    // sitting alongside it, so leaving the zone tier off would mean a zone change
    // announced by nobody.
    public bool ZoneNotificationEnabled { get; set; } = true;

    public bool AreaNotificationEnabled { get; set; } = true;

    public bool SubAreaNotificationEnabled { get; set; } = true;

    public bool IncludeParentTierAsHeader { get; set; } = true;

    // --- the game's own text ------------------------------------------------

    // The game's own "_AreaText" flash covers the same ground as this plugin, and
    // renders underneath it. On by default, since showing both is never wanted.
    public bool HideNativeAreaText { get; set; } = true;

    // Suppresses the loading-screen zone title, which the game splits across two
    // addons: "_LocationTitle" and "_LocationTitleShort".
    public bool HideNativeLoadingTitle { get; set; } = true;

    // --- placement ----------------------------------------------------------

    // A percentage of viewport height.
    public float VerticalPosition { get; set; } = 25f;

    // The same, across the viewport's width. The text is centred on this point
    // rather than starting at it, so the value is an anchor and not a left edge —
    // a long place name at 15% still reaches back past the anchor, and off screen
    // if it is long enough. Left as a plain percentage for that reason: an
    // alignment enum would promise an edge-safety this does not have.
    public float HorizontalPosition { get; set; } = 50f;

    // --- type ---------------------------------------------------------------

    // Large enough to read as a title rather than as a label. No ceiling applies
    // at the default font, which is vector; and 91 px is exactly TrumpGothic's
    // ceiling, so switching to the FFXIV display face keeps it sharp too. Jupiter
    // and Axis run out well below this and will say so.
    public float DisplayFontSize { get; set; } = 91f;

    public float HeaderFontSize { get; set; } = 24f;

    // Noto: the only choice that needs no caveat, being sharp at any size and
    // carrying glyphs for every language the client can display. The three game
    // faces look more like FFXIV and are a click away.
    public DisplayFontChoice DisplayFont { get; set; } = DisplayFontChoice.NotoSansCjk;

    // Extra space between glyphs, as a percentage of the font's own size rather
    // than a count of pixels. One value therefore tracks both lines despite the
    // header being a third the size, and stays in proportion when the size slider
    // moves. Zero is ImGui's own spacing.
    public float LetterSpacing { get; set; } = 0f;

    // Cased with ToUpperInvariant, never ToUpper: on a Turkish system the
    // culture-sensitive form turns "i" into "İ", so "Limsa Lominsa" would upcase
    // differently for those players alone. Place names come out of the game's own
    // data and should not shift with the operating system's locale.
    public bool UppercaseText { get; set; } = false;

    public bool UnderlineHeader { get; set; } = true;

    public bool OverlapHeader { get; set; } = true;

    // --- effects ------------------------------------------------------------

    // Eorzean -> Latin decode during the reveal. Silently inert when no font is
    // bundled, or when the text falls outside the font's Latin coverage.
    //
    // Kept as its own switch rather than folded in as one value of MotionEffect:
    // the decode is what the plugin is *for*, it composes with every motion
    // rather than competing with them, and presets deliberately leave it alone so
    // that turning it off stays one decision made in one place.
    public bool DecodeEffectEnabled { get; set; } = true;

    // None is the plugin as it shipped: a decode and no movement.
    public MotionEffect Motion { get; set; } = MotionEffect.None;

    // Off by default: this is a location notification first, and hearts drifting
    // off every sub-area change is a taste, not a default.
    public ParticleEffect Particles { get; set; } = ParticleEffect.None;

    // Multiplies the spawn rate. The per-effect rates are chosen so that 1 reads
    // as "a few", not as weather.
    public float ParticleDensity { get; set; } = 1f;

    // One colour for whichever effect is on. A warm amber suits embers and
    // sparkles, which are the two that look wrong in an arbitrary hue; hearts and
    // petals want to be moved towards pink, and the config window says so.
    public Vector4 ParticleColor { get; set; } = new(1f, 0.72f, 0.35f, 1f);

    // --- colour -------------------------------------------------------------

    // Vector4 rather than a packed uint: it is what ImGui's colour pickers take,
    // and it stays legible if anyone opens the config file.
    public Vector4 TextColor { get; set; } = new(0.875f, 0.761f, 0.584f, 1f);

    public Vector4 HeaderColor { get; set; } = new(0.698f, 0.627f, 0.569f, 1f);

    public Vector4 StrokeColor { get; set; } = new(0f, 0f, 0f, 0.8f);

    // Multiplies how far the 8-way stroke is stamped from the glyph. 1 is the
    // weight that shipped. Zero drops the stroke entirely rather than drawing it
    // at zero distance, so turning the outline off costs no draw calls and needs
    // no separate checkbox.
    public float StrokeThickness { get; set; } = 1f;

    // --- saved presets -------------------------------------------------------

    // Looks the user saved, in the order they created them.
    //
    // Kept alongside the built-ins in Presets.All rather than merged with them:
    // the built-ins are code and cannot be edited or removed, these are data and
    // can be both. Applying either writes the same ten settings, so once applied
    // there is no way to tell — and nothing needs to.
    public List<UserPreset> UserPresets { get; set; } = [];

    // --- durations ----------------------------------------------------------

    public TimeSpan FadeInDuration { get; set; } = TimeSpan.FromSeconds(0.9);

    // The motion, which runs first and on its own: the line arrives in Eorzean
    // script, lands, and only then decodes. Long enough that a rise or a burn is
    // something you watch rather than something you catch the end of.
    //
    // Inert when the motion is None, and skipped outright — the notification does
    // not sit through a stage with nothing to show.
    public TimeSpan MotionDuration { get; set; } = TimeSpan.FromSeconds(1.1);

    // The decode, which follows it. Slower than the 0.9s this shipped with: with
    // the motion no longer running at the same time, the decode is the only thing
    // happening and reads better given room.
    public TimeSpan RevealDuration { get; set; } = TimeSpan.FromSeconds(1.3);

    public TimeSpan ShowDuration { get; set; } = TimeSpan.FromSeconds(4);

    public TimeSpan FadeOutDuration { get; set; } = TimeSpan.FromSeconds(2);

    // --- when to stay quiet -------------------------------------------------

    // Combat and duties made sense while the plugin drew on top of the game's own
    // notices — silencing it still left the native text. Now that it replaces
    // that text, suppressing here means no location feedback at all, which is
    // worse than the distraction it was guarding against. They are also exactly
    // when sub-areas change most.
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

        // Nothing here can know what a later shape meant, and rewriting the file
        // would discard whatever that build stored without being able to put it
        // back if the user upgrades again. Deserialization has already filled
        // anything unreadable with defaults, so run with what we got and leave the
        // file alone.
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
