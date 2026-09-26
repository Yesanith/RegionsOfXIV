using RegionsOfXIV.Services;

namespace RegionsOfXIV.Tests;

// Which of the game's sounds each category plays: its own when it has one, the one chosen for
// places when it does not.
public class SoundCategoryTests
{
    private readonly List<uint> played = [];

    private readonly TestClock clock = new();

    private NotificationSounds Sounds(FakeSoundSettings settings) =>
        new(settings, this.clock.Read, play: id => this.played.Add(id));

    [Fact]
    public void WeatherFollowsPlacesUntilGivenASoundOfItsOwn()
    {
        var settings = new FakeSoundSettings { GameSoundId = 7 };

        Sounds(settings).Play(SoundCategory.Weather);

        Assert.Equal([7u], this.played);
    }

    [Fact]
    public void WeatherPlaysItsOwnSoundWhenItHasOne()
    {
        var settings = new FakeSoundSettings { GameSoundId = 7, GameSoundIdWeather = 3 };

        Sounds(settings).Play(SoundCategory.Weather);

        Assert.Equal([3u], this.played);
    }

    [Fact]
    public void BannersPlayTheirOwnSoundWhenTheyHaveOne()
    {
        var settings = new FakeSoundSettings { GameSoundId = 7, GameSoundIdBanner = 12 };

        Sounds(settings).Play(SoundCategory.Banner);

        Assert.Equal([12u], this.played);
    }

    [Fact]
    public void PlacesNeverBorrowAnotherCategorysSound()
    {
        var settings = new FakeSoundSettings { GameSoundId = 7, GameSoundIdWeather = 3, GameSoundIdBanner = 12 };

        Sounds(settings).Play(SoundCategory.Location);

        Assert.Equal([7u], this.played);
    }

    [Fact]
    public void TheAuditionButtonPlaysTheCategoryItIsBeside()
    {
        var settings = new FakeSoundSettings { GameSoundId = 7, GameSoundIdBanner = 12 };

        Sounds(settings).PlayNow(SoundCategory.Banner);

        Assert.Equal([12u], this.played);
    }
}
