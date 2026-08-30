using RegionsOfXIV.Services;

namespace RegionsOfXIV.Tests;

// BannerWatcher decides once, when the icon on an addon changes, and then hides the game's banner
// from that decision on every later frame. These cover the remembering half. The call order inside
// OnImage cannot be reached from here -- it dereferences addon pointers -- so the rule that the
// gate is consulted only on a transition still has to be checked in game.
public class BannerDecisionsTests
{
    private readonly BannerDecisions decisions = new();

    private const nint Image = 0x1000;
    private const nint Image2 = 0x2000;

    private const uint LevelUp = 120109;
    private const uint QuestAccepted = 120001;

    [Fact]
    public void AnAddonNothingIsKnownAboutIsANewBanner()
    {
        Assert.True(this.decisions.IsNew(Image, LevelUp));
    }

    [Fact]
    public void TheSameIconOnTheNextFrameIsNotANewBanner()
    {
        this.decisions.Record(Image, LevelUp, taken: true);

        Assert.False(this.decisions.IsNew(Image, LevelUp));
    }

    [Fact]
    public void ADifferentIconOnTheSameAddonIsANewBanner()
    {
        this.decisions.Record(Image, LevelUp, taken: true);

        Assert.True(this.decisions.IsNew(Image, QuestAccepted));
    }

    [Fact]
    public void TheAddonGoingBlankIsANewBanner()
    {
        this.decisions.Record(Image, LevelUp, taken: true);

        Assert.True(this.decisions.IsNew(Image, 0));
    }

    [Fact]
    public void TheSameIconReturningAfterAnotherIsANewBanner()
    {
        this.decisions.Record(Image, LevelUp, taken: true);
        this.decisions.Record(Image, QuestAccepted, taken: true);

        Assert.True(this.decisions.IsNew(Image, LevelUp));
    }

    // The regression this whole structure exists to prevent: the gate says yes once, and every
    // frame after that has to keep hiding the game's banner even though the gate would now say no.
    [Fact]
    public void ATakenBannerStaysTakenForAsLongAsItIsShowing()
    {
        this.decisions.Record(Image, LevelUp, taken: true);

        for (var frame = 0; frame < 120; frame++)
        {
            Assert.False(this.decisions.IsNew(Image, LevelUp));
            Assert.True(this.decisions.IsTaken(Image));
        }
    }

    // And the other way round, which is the bug being fixed: a banner the gate refused must not
    // start being hidden partway through.
    [Fact]
    public void ARefusedBannerStaysRefusedForAsLongAsItIsShowing()
    {
        this.decisions.Record(Image, LevelUp, taken: false);

        for (var frame = 0; frame < 120; frame++)
            Assert.False(this.decisions.IsTaken(Image));
    }

    [Fact]
    public void NothingIsTakenOnAnAddonNothingIsKnownAbout()
    {
        Assert.False(this.decisions.IsTaken(Image));
    }

    // Four _Image addons exist and any of them can hold a banner, so one being taken over says
    // nothing about the others.
    [Fact]
    public void EachAddonIsRememberedSeparately()
    {
        this.decisions.Record(Image, LevelUp, taken: true);
        this.decisions.Record(Image2, QuestAccepted, taken: false);

        Assert.True(this.decisions.IsTaken(Image));
        Assert.False(this.decisions.IsTaken(Image2));
        Assert.False(this.decisions.IsNew(Image, LevelUp));
        Assert.False(this.decisions.IsNew(Image2, QuestAccepted));
    }

    [Fact]
    public void ReRecordingTheSameIconReplacesTheDecision()
    {
        this.decisions.Record(Image, LevelUp, taken: true);
        this.decisions.Record(Image, LevelUp, taken: false);

        Assert.False(this.decisions.IsTaken(Image));
    }

    // Switching banners off clears the memory, so switching them back on re-decides rather than
    // acting on an answer from before the setting changed.
    [Fact]
    public void ClearingForgetsEverything()
    {
        this.decisions.Record(Image, LevelUp, taken: true);
        this.decisions.Record(Image2, QuestAccepted, taken: true);

        this.decisions.Clear();

        Assert.Equal(0, this.decisions.Count);
        Assert.True(this.decisions.IsNew(Image, LevelUp));
        Assert.False(this.decisions.IsTaken(Image));
    }

    [Fact]
    public void CountTracksHowManyAddonsAreRemembered()
    {
        Assert.Equal(0, this.decisions.Count);

        this.decisions.Record(Image, LevelUp, taken: true);
        Assert.Equal(1, this.decisions.Count);

        this.decisions.Record(Image, QuestAccepted, taken: true);
        Assert.Equal(1, this.decisions.Count);

        this.decisions.Record(Image2, QuestAccepted, taken: true);
        Assert.Equal(2, this.decisions.Count);
    }
}
