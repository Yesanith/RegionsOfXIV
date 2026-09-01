using RegionsOfXIV.Services;

namespace RegionsOfXIV.Tests;

// The rules that stand in for the game's mixer on the file playback path.
//
// This is the part of the sound feature most worth testing, because getting it wrong is not a
// missing sound but a sound in a room somebody deliberately made quiet, and because the meaning of
// every flag it reads was established by observation rather than from documentation. A test that
// pins the observed polarity is what stops the next reader "fixing" IsSndSystem back to front on
// the strength of what its name looks like it says.
//
// In the Localization collection because a refusal comes back with a reason string, and those come
// from Loc, which LocalizationTests swaps about.
[Collection("Localization")]
public class GameMixerRulesTests
{
    [Fact]
    public void AnUntouchedClientPlaysAtFullVolume()
    {
        Assert.Equal(1f, GameMixerRules.Decide(new FakeGameAudio()).Volume);
    }

    [Fact]
    public void FullVolumeNeedsNoExplaining()
    {
        Assert.Null(GameMixerRules.Decide(new FakeGameAudio()).Reason);
    }

    // 24 was the master volume on the client the flags were mapped against, which is why it is the
    // number here: it is the case that first showed the scaling reaching NAudio at all.
    [Fact]
    public void TheTwoVolumesMultiply()
    {
        var audio = new FakeGameAudio { MasterVolume = 24, SystemVolume = 50 };

        Assert.Equal(0.12f, GameMixerRules.Decide(audio).Volume);
    }

    [Fact]
    public void AVolumeAboveTheKnownRangeIsHeldAtFull()
    {
        var audio = new FakeGameAudio { MasterVolume = 400 };

        Assert.Equal(1f, GameMixerRules.Decide(audio).Volume);
    }

    // The polarity that was verified in game: the checkbox writes 1, and 1 is muted. Reading it the
    // other way round would play only while the player had asked for silence.
    [Fact]
    public void MutingSystemSoundsStopsIt()
    {
        Assert.Null(GameMixerRules.Decide(new FakeGameAudio { SystemMuted = true }).Volume);
    }

    [Fact]
    public void MutingTheMasterStopsIt()
    {
        Assert.Null(GameMixerRules.Decide(new FakeGameAudio { MasterMuted = true }).Volume);
    }

    [Fact]
    public void SoundTurnedOffEntirelyStopsIt()
    {
        Assert.Null(GameMixerRules.Decide(new FakeGameAudio { SoundDisabled = true }).Volume);
    }

    [Fact]
    public void EitherVolumeAtZeroStopsIt()
    {
        Assert.Null(GameMixerRules.Decide(new FakeGameAudio { SystemVolume = 0 }).Volume);
    }

    [Fact]
    public void SettingsThatCannotBeReadStopIt()
    {
        Assert.Null(GameMixerRules.Decide(new FakeGameAudio { Readable = false }).Volume);
    }

    // Unreadable is answered before anything else, because every other reading is meaningless
    // while it holds, and a player who sees "the game is muted" at the title screen would go and
    // check a mute that is not set.
    [Fact]
    public void SettingsThatCannotBeReadSayThatRatherThanMuted()
    {
        var audio = new FakeGameAudio { Readable = false, SystemMuted = true };

        Assert.Contains("cannot be read", GameMixerRules.Decide(audio).Reason);
    }

    [Fact]
    public void ARefusalAlwaysExplainsItself()
    {
        Assert.NotNull(GameMixerRules.Decide(new FakeGameAudio { SystemMuted = true }).Reason);
    }

    // The conservative reading of the unfocused flags: both have to allow it. Which of the two the
    // game itself consults is not known, and the two ways of being wrong are not equal, so the one
    // that costs a sound nobody was listening for is the one taken.
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void UnfocusedNeedsBothFlags(bool overall, bool system, bool plays)
    {
        var audio = new FakeGameAudio
        {
            WindowFocused = false,
            UnfocusedSoundAllowed = overall,
            UnfocusedSystemSoundAllowed = system,
        };

        Assert.Equal(plays, GameMixerRules.Decide(audio).Volume != null);
    }

    // Focused is the ordinary case and neither flag applies to it. A client with the feature off
    // holds 0 in all of them, so consulting them while the window is in front would silence the
    // plugin for most players.
    [Fact]
    public void FocusedIgnoresTheUnfocusedFlagsEntirely()
    {
        var audio = new FakeGameAudio
        {
            WindowFocused = true,
            UnfocusedSoundAllowed = false,
            UnfocusedSystemSoundAllowed = false,
        };

        Assert.Equal(1f, GameMixerRules.Decide(audio).Volume);
    }
}

// Defaults are an untouched client with the window in front, so every test names only the one
// thing it is about.
internal sealed class FakeGameAudio : IGameAudio
{
    public bool Readable { get; set; } = true;

    public bool SoundDisabled { get; set; }

    public bool MasterMuted { get; set; }

    public bool SystemMuted { get; set; }

    public int MasterVolume { get; set; } = 100;

    public int SystemVolume { get; set; } = 100;

    public bool WindowFocused { get; set; } = true;

    public bool UnfocusedSoundAllowed { get; set; }

    public bool UnfocusedSystemSoundAllowed { get; set; }
}
