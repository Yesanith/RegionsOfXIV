using System;
using RegionsOfXIV.Models;

namespace RegionsOfXIV.Services;

// Every interface the announcement path talks through, in one place: what reaches it (the
// sources below), where its decisions go (INotificationSink), what it may ask about the client
// (IGameState) and the slice of the config the gate can see (IGateSettings).
//
// Each has one real implementation backed by the game -- GameSources.cs for most of them -- and
// one fake in the tests, which is what keeps the announcement rules testable without launching
// FFXIV. Nothing here should ever gain a Dalamud type in its signature; that is the whole point
// of the boundary, and ZoneArrival exists so Dalamud's own event argument does not cross it.
internal readonly record struct AnnouncementSources(
    ILocationSource Locations,
    IWeatherSource Weather,
    IAreaTextSource AreaText,
    IBannerSource Banners,
    IZoneArrivals Zones,
    IPlaceNames PlaceNames,
    IWeatherNames WeatherNames);

internal interface ILocationSource
{
    event Action<LocationSnapshot, LocationSnapshot>? OnLocationChanged;

    event Action<bool>? OnSanctuaryChanged;

    LocationSnapshot Current { get; }

    float Speed { get; }

    void Poll();
}

internal interface IWeatherSource
{
    event Action<byte>? OnWeatherChanged;

    byte Current { get; }

    void Prime(uint weatherId);

    void Reset();
}

internal interface IAreaTextSource
{
    event Action<string?>? OnAreaTextShown;
}

// The banner's icon, its wording, and why the gate would refuse it -- BannerBlock.None when it
// would not. The reason travels with the event because the watcher has to know it first, to decide
// whether to hide the game's own banner; asking a second time on the other side would be a second
// read of the clock, and a cooldown expiring between the two would show both banners at once.
internal interface IBannerSource
{
    event Action<uint, string, BannerBlock>? OnBannerShown;
}

internal readonly record struct ZoneArrival(
    uint TerritoryTypeId,
    uint PlaceNameId,
    uint ZonePlaceNameId,
    uint RegionPlaceNameId,
    bool IsPvp,
    bool IsDuty);

internal interface IZoneArrivals
{
    event Action<ZoneArrival>? Arrived;

    event Action? LoggedOut;
}

internal interface IPlaceNames
{
    string? Resolve(uint placeNameRowId);

    ResolvedLocation Resolve(in LocationSnapshot snapshot);
}

internal interface IWeatherNames
{
    ResolvedWeather? Resolve(uint weatherRowId);

    ResolvedWeather? Forecast(uint territoryTypeId, DateTimeOffset at);
}

// The slice of Configuration that NotificationGate is allowed to see. Configuration implements
// it directly; the point is that the gate cannot reach a colour or a font and quietly grow a
// dependency on how things look.
internal interface IGateSettings
{
    bool ZoneNotificationEnabled { get; }

    bool AreaNotificationEnabled { get; }

    bool SubAreaNotificationEnabled { get; }

    bool HideNativeLoadingTitle { get; }

    bool HideInCombat { get; }

    bool HideInDuty { get; }

    bool HideWhileTravellingFast { get; }

    bool WeatherNotificationEnabled { get; }

    bool BannerNotificationEnabled { get; }
}

// The slice of the config NotificationSounds can see, narrow for the same reason IGateSettings is:
// it lets the interval rule be exercised against a fake without a Configuration or a game.
internal interface ISoundSettings
{
    SoundSource SoundSource { get; }

    int GameSoundId { get; }

    string SoundFilePath { get; }

    bool SoundOnLocation { get; }

    bool SoundOnWeather { get; }

    bool SoundOnBanner { get; }
}

// The game's own sound settings, as far as playing a file has to care about them.
//
// Only the file path reads any of this. A game sound is mixed by the game and obeys all of it
// without being asked; a file goes out through NAudio into the process's Windows audio session,
// where none of it applies unless something puts it back. GameMixerRules is that something, and
// this is the interface that lets it be tested without a client.
//
// The volumes are the raw 0 to 100 the game stores. The mute flags are true when muted, which is
// the way round the client was observed to write them rather than the way the names read.
internal interface IGameAudio
{
    // False when the options could not be read at all, which is the state before the game's config
    // is up. Separate from the settings themselves so that "not known" is not silently answered as
    // "not muted, full volume".
    bool Readable { get; }

    bool SoundDisabled { get; }

    bool MasterMuted { get; }

    bool SystemMuted { get; }

    int MasterVolume { get; }

    int SystemVolume { get; }

    bool WindowFocused { get; }

    bool UnfocusedSoundAllowed { get; }

    bool UnfocusedSystemSoundAllowed { get; }
}

internal readonly record struct NotificationTiming(TimeSpan UntilReadable, TimeSpan OnScreen);

// Where an announcement goes once it has been decided on. The overlay implements it; the tests
// substitute a fake, which is what keeps AnnouncementCoordinator runnable without a screen.
//
// Timing comes back the other way because the gate needs to know how long the thing it just
// pushed will be readable before it will allow another one.
internal interface INotificationSink
{
    void Push(string? header, string text);

    void PushWeather(string text, uint iconId);

    // Separate from Push because a banner and a place name can be on screen at the same moment.
    // Light Party arrives about two milliseconds after the zone arrival that admits you to the
    // duty, and through Push the later of the two dismissed the earlier.
    void PushBanner(string text);

    NotificationTiming Timing { get; }
}

// What the gate is allowed to ask about the client. DalamudGameState, over in GameSources.cs,
// is the only thing that knows these are ConditionFlags.
internal interface IGameState
{
    bool IsLoggedIn { get; }

    bool IsBetweenAreas { get; }

    bool IsInCutscene { get; }

    bool IsPvP { get; }

    bool IsGPosing { get; }

    bool IsInCombat { get; }

    bool IsBoundByDuty { get; }
}
