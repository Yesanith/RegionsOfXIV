using RegionsOfXIV;
using RegionsOfXIV.Services;

namespace RegionsOfXIV.Tests;

// The interval rule and the category toggles, which are the whole of the decision. Playback
// itself is UIGlobals.PlayChatSoundEffect and needs a running client, so it is substituted here
// rather than tested: there is nothing to assert about it that would not be asserting the fake.
public class NotificationSoundsTests
{
    private readonly FakeSoundSettings settings = new();
    private readonly TestClock clock = new();
    private readonly List<uint> played = [];
    private readonly List<string> files = [];

    // What the real file player answers when it decides not to play: the game is muted, the
    // window is not in front, or the file could not be read.
    private bool fileAccepted = true;

    private NotificationSounds Sounds() =>
        new(this.settings, this.clock.Read, this.played.Add, PlayFile);

    private bool PlayFile(string path)
    {
        this.files.Add(path);
        return this.fileAccepted;
    }

    [Fact]
    public void SilentByDefault()
    {
        Assert.False(new NotificationSounds(new FakeSoundSettings { SoundSource = SoundSource.Off })
            .Play(SoundCategory.Location));
    }

    [Fact]
    public void APushMakesASound()
    {
        Assert.True(Sounds().Play(SoundCategory.Location));
        Assert.Single(this.played);
    }

    // The case the interval exists for. An arrival announces its place name and its weather in the
    // same call, and a party banner lands about two milliseconds behind a zone arrival.
    [Fact]
    public void TwoLanesArrivingTogetherMakeOneSound()
    {
        var sounds = Sounds();

        Assert.True(sounds.Play(SoundCategory.Location));
        Assert.False(sounds.Play(SoundCategory.Weather));

        Assert.Single(this.played);
    }

    [Fact]
    public void ASoundAfterTheGapIsAllowed()
    {
        var sounds = Sounds();
        sounds.Play(SoundCategory.Location);

        this.clock.Advance(0.2);
        Assert.False(sounds.Play(SoundCategory.Banner));

        this.clock.Advance(0.1);
        Assert.True(sounds.Play(SoundCategory.Banner));
    }

    // Dropped, not queued, and dropping must not push the window along either. A burst of pushes
    // would otherwise hold the gap open for as long as the burst lasted.
    [Fact]
    public void ADroppedSoundDoesNotExtendTheWindow()
    {
        var sounds = Sounds();
        sounds.Play(SoundCategory.Location);

        this.clock.Advance(0.2);
        sounds.Play(SoundCategory.Weather);
        sounds.Play(SoundCategory.Weather);

        this.clock.Advance(0.06);
        Assert.True(sounds.Play(SoundCategory.Banner));
    }

    // Named by string rather than by the enum, because SoundCategory is internal and xUnit only
    // discovers public methods. The same shape BannersAreSuppressedByTheUnconditionalRules uses.
    [Theory]
    [InlineData("location")]
    [InlineData("weather")]
    [InlineData("banner")]
    public void ACategoryCanBeSwitchedOffOnItsOwn(string name)
    {
        var off = CategoryNamed(name);

        this.settings.SoundOnLocation = off != SoundCategory.Location;
        this.settings.SoundOnWeather = off != SoundCategory.Weather;
        this.settings.SoundOnBanner = off != SoundCategory.Banner;

        var sounds = Sounds();

        Assert.False(sounds.Play(off));
        Assert.Empty(this.played);
    }

    private static SoundCategory CategoryNamed(string name) => name switch
    {
        "location" => SoundCategory.Location,
        "weather" => SoundCategory.Weather,
        _ => SoundCategory.Banner,
    };

    // A category that is switched off is refused before the interval is consulted, so it cannot
    // silence the category after it.
    [Fact]
    public void ASwitchedOffCategoryDoesNotConsumeTheWindow()
    {
        this.settings.SoundOnLocation = false;

        var sounds = Sounds();

        Assert.False(sounds.Play(SoundCategory.Location));
        Assert.True(sounds.Play(SoundCategory.Weather));
    }

    // This was silence while File was a value with nothing behind it. It is now handed straight to
    // the file player, even with no path stored, because that is where every reason not to play a
    // file lives: the path check, the game's own sound settings, and the decode. Refusing an empty
    // path here as well would be a second copy of the first of those.
    [Fact]
    public void AFileSourceIsHandedToThePlayerRatherThanRefusedHere()
    {
        this.settings.SoundSource = SoundSource.File;

        Sounds().Play(SoundCategory.Location);

        Assert.Equal(string.Empty, Assert.Single(this.files));
    }

    // The audition button. Hearing the sound you just picked is the point of it.
    [Fact]
    public void PlayNowIgnoresTheInterval()
    {
        var sounds = Sounds();
        sounds.Play(SoundCategory.Location);

        sounds.PlayNow();

        Assert.Equal(2, this.played.Count);
    }

    [Fact]
    public void PlayNowIsSilentWhileSoundIsOff()
    {
        this.settings.SoundSource = SoundSource.Off;

        Sounds().PlayNow();

        Assert.Empty(this.played);
    }

    // The stored number is what the player reads, "<se.1>" through "<se.16>". A number outside
    // that can only come from a hand-edited config, and the nearest sound beats none.
    [Theory]
    [InlineData(1, 1u)]
    [InlineData(16, 16u)]
    [InlineData(0, 1u)]
    [InlineData(-4, 1u)]
    [InlineData(99, 16u)]
    public void TheStoredNumberIsClampedToTheSixteen(int stored, uint expected)
    {
        this.settings.GameSoundId = stored;

        Sounds().Play(SoundCategory.Location);

        Assert.Equal(expected, Assert.Single(this.played));
    }

    // The file source goes to the file player and nowhere near the game's chat effects. Both
    // sources are reached through the same interval and the same category toggles, which is why
    // only the last step of the decision changes between them.
    [Fact]
    public void AFileSourcePlaysTheFileRatherThanAGameSound()
    {
        this.settings.SoundSource = SoundSource.File;
        this.settings.SoundFilePath = @"C:\sounds\arrival.wav";

        Assert.True(Sounds().Play(SoundCategory.Location));

        Assert.Equal(@"C:\sounds\arrival.wav", Assert.Single(this.files));
        Assert.Empty(this.played);
    }

    // The player refusing is not an error and not a retry. It happens whenever the game is muted,
    // which is an ordinary state, so it reads back as "no sound was made" and nothing more.
    [Fact]
    public void AFileTheGameSettingsRefuseCountsAsNoSound()
    {
        this.settings.SoundSource = SoundSource.File;
        this.settings.SoundFilePath = @"C:\sounds\arrival.wav";
        this.fileAccepted = false;

        Assert.False(Sounds().Play(SoundCategory.Location));
        Assert.Single(this.files);
    }

    // The interval is spent before the file is attempted, so a broken file costs the same quarter
    // second as a good one. Without that, a file that always fails would be opened once per
    // announcement rather than four times a second at most.
    [Fact]
    public void TheIntervalCoversFilesAsWellAsGameSounds()
    {
        this.settings.SoundSource = SoundSource.File;
        this.settings.SoundFilePath = @"C:\sounds\arrival.wav";
        this.fileAccepted = false;

        var sounds = Sounds();

        sounds.Play(SoundCategory.Location);
        sounds.Play(SoundCategory.Weather);

        Assert.Single(this.files);
    }

    // Only a test constructs one of these without a file player. In the plugin Plugin.cs always
    // supplies one, so this is asserting that the missing case is silence rather than a throw.
    [Fact]
    public void WithNoFilePlayerWiredAFileSourceIsSilent()
    {
        var settings = new FakeSoundSettings
        {
            SoundSource = SoundSource.File,
            SoundFilePath = @"C:\sounds\arrival.wav",
        };

        Assert.False(new NotificationSounds(settings).Play(SoundCategory.Location));
    }

    [Fact]
    public void TheAuditionButtonPlaysAFileToo()
    {
        this.settings.SoundSource = SoundSource.File;
        this.settings.SoundFilePath = @"C:\sounds\arrival.wav";

        Sounds().PlayNow();

        Assert.Single(this.files);
    }

    // Off is checked before the source is looked at, so a stored path cannot make a noise while
    // the setting says none.
    [Fact]
    public void OffBeatsAStoredFilePath()
    {
        this.settings.SoundSource = SoundSource.Off;
        this.settings.SoundFilePath = @"C:\sounds\arrival.wav";

        Assert.False(Sounds().Play(SoundCategory.Location));
        Assert.Empty(this.files);
    }

    [Fact]
    public void ACategoryTurnedOffSilencesAFileToo()
    {
        this.settings.SoundSource = SoundSource.File;
        this.settings.SoundFilePath = @"C:\sounds\arrival.wav";
        this.settings.SoundOnBanner = false;

        Assert.False(Sounds().Play(SoundCategory.Banner));
        Assert.Empty(this.files);
    }
}

internal sealed class FakeSoundSettings : ISoundSettings
{
    public SoundSource SoundSource { get; set; } = SoundSource.GameSound;

    public int GameSoundId { get; set; } = 1;

    public string SoundFilePath { get; set; } = string.Empty;

    public bool SoundOnLocation { get; set; } = true;

    public bool SoundOnWeather { get; set; } = true;

    public bool SoundOnBanner { get; set; } = true;
}
