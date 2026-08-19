using RegionsOfXIV;

namespace RegionsOfXIV.Tests;

public class ChangelogTests
{
    [Fact]
    public void NewestEntryMatchesTheVersionThisWasBuiltAs()
    {
        Assert.NotEmpty(Changelog.All);
        Assert.Equal(Changelog.Current, Changelog.All[0].Version);
    }

    [Fact]
    public void EntriesRunNewestFirst()
    {
        for (var i = 1; i < Changelog.All.Length; i++)
            Assert.True(
                Changelog.All[i - 1].Version > Changelog.All[i].Version,
                $"{Changelog.All[i - 1].Version} should sort above {Changelog.All[i].Version}.");
    }

    [Fact]
    public void EveryEntrySaysSomething()
    {
        foreach (var entry in Changelog.All)
        {
            Assert.NotEmpty(entry.Changes);
            Assert.All(entry.Changes, line => Assert.False(string.IsNullOrWhiteSpace(line)));
        }
    }

    [Fact]
    public void ShowsNothingWhenAlreadyOnTheCurrentVersion()
    {
        Assert.Empty(Changelog.Since(Changelog.Current));
    }

    [Fact]
    public void ShowsOnlyTheNewestWhenNothingWasRecorded()
    {
        Assert.Single(Changelog.Since(null));
        Assert.Equal(Changelog.All[0].Version, Changelog.Since(null)[0].Version);
    }

    [Fact]
    public void ShowsEverythingSinceTheVersionRecorded()
    {
        if (Changelog.All.Length < 3)
            return;

        var twoBack = Changelog.All[2].Version;
        var shown = Changelog.Since(twoBack);

        Assert.Equal(2, shown.Length);
        Assert.Equal(Changelog.All[0].Version, shown[0].Version);
        Assert.Equal(Changelog.All[1].Version, shown[1].Version);
    }

    [Fact]
    public void ShowsNothingForAVersionAheadOfThisBuild()
    {
        Assert.Empty(Changelog.Since(new Version(99, 0, 0, 0)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a version")]
    [InlineData("0.2")]
    public void ParseIsToleranceRatherThanValidation(string? stored)
    {
        var parsed = Changelog.Parse(stored);
        Assert.True(parsed == null || parsed == new Version("0.2"));
    }
}
