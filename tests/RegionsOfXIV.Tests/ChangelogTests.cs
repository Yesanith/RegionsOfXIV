using RegionsOfXIV;

namespace RegionsOfXIV.Tests;

// The changelog's failure mode is drift: the version in the csproj moves and the
// entry does not, or the other way round. Nothing about that is visible at
// compile time and nobody notices in game, because the symptom is a window that
// quietly does not appear.
public class ChangelogTests
{
    // The one that matters. Bumping <Version> without writing an entry means the
    // release ships with nothing to say for itself; adding an entry for a version
    // that was never built means everyone on the current build is shown a
    // changelog for a release they do not have.
    [Fact]
    public void NewestEntryMatchesTheVersionThisWasBuiltAs()
    {
        Assert.NotEmpty(Changelog.All);
        Assert.Equal(Changelog.Current, Changelog.All[0].Version);
    }

    // Since takes All[0] as "the newest" and filters the rest by comparison, so
    // both of those depend on the order being right.
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

    // The ordinary load: already up to date, so nothing to show.
    [Fact]
    public void ShowsNothingWhenAlreadyOnTheCurrentVersion()
    {
        Assert.Empty(Changelog.Since(Changelog.Current));
    }

    // A config written before this window existed. No way to know what they last
    // ran, so the newest entry alone rather than the whole history.
    [Fact]
    public void ShowsOnlyTheNewestWhenNothingWasRecorded()
    {
        Assert.Single(Changelog.Since(null));
        Assert.Equal(Changelog.All[0].Version, Changelog.Since(null)[0].Version);
    }

    // Someone who skipped a release gets both, still newest first.
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

    // A version newer than anything here — a downgrade, or a hand-edited config.
    // Nothing to show beats showing a release they have already had.
    [Fact]
    public void ShowsNothingForAVersionAheadOfThisBuild()
    {
        Assert.Empty(Changelog.Since(new Version(99, 0, 0, 0)));
    }

    // The value is read from a file a person can open and edit.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a version")]
    [InlineData("0.2")]
    public void ParseIsToleranceRatherThanValidation(string? stored)
    {
        // "0.2" is a legal Version; the rest are not. Either way it must not throw.
        var parsed = Changelog.Parse(stored);
        Assert.True(parsed == null || parsed == new Version("0.2"));
    }
}
