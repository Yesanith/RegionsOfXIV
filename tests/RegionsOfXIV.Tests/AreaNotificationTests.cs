using RegionsOfXIV.UI;

namespace RegionsOfXIV.Tests;

public class AreaNotificationTests
{
    private static AreaNotification Line(string? header, string text) => new(
        header,
        text,
        TimeSpan.FromSeconds(1),
        TimeSpan.Zero,
        TimeSpan.Zero,
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(1));

    [Fact]
    public void TextIsReadableBeforeAnyCasingIsAsked()
    {
        var notification = Line("Middle La Noscea", "Summerford Farms");

        notification.ApplyCasing(false);

        Assert.Equal("Summerford Farms", notification.CasedText);
        Assert.Equal("Middle La Noscea", notification.CasedHeader);
    }

    [Fact]
    public void UppercasingCoversBothLines()
    {
        var notification = Line("Middle La Noscea", "Summerford Farms");

        notification.ApplyCasing(true);

        Assert.Equal("SUMMERFORD FARMS", notification.CasedText);
        Assert.Equal("MIDDLE LA NOSCEA", notification.CasedHeader);
    }

    [Fact]
    public void TurningUppercaseBackOffRestoresTheOriginal()
    {
        var notification = Line("Middle La Noscea", "Summerford Farms");

        notification.ApplyCasing(true);
        notification.ApplyCasing(false);

        Assert.Equal("Summerford Farms", notification.CasedText);
        Assert.Equal("Middle La Noscea", notification.CasedHeader);
    }

    [Fact]
    public void AskingForTheSameCasingTwiceKeepsTheSameStringSoLayoutsStayCached()
    {
        var notification = Line(null, "Summerford Farms");

        notification.ApplyCasing(true);
        var first = notification.CasedText;

        notification.ApplyCasing(true);

        Assert.Same(first, notification.CasedText);
    }

    [Fact]
    public void ALineWithNoHeaderCasesToNothingRatherThanNull()
    {
        var notification = Line(null, "Summerford Farms");

        notification.ApplyCasing(true);

        Assert.Equal(string.Empty, notification.CasedHeader);
    }

    [Fact]
    public void AHeaderWithNoTextBecomesTheText()
    {
        var notification = Line("Middle La Noscea", string.Empty);

        notification.ApplyCasing(false);

        Assert.Equal("Middle La Noscea", notification.CasedText);
        Assert.Equal(string.Empty, notification.CasedHeader);
    }

    [Fact]
    public void RecasingClearsTheCipherSoItIsRebuiltForTheNewLetters()
    {
        var notification = Line(null, "Summerford Farms");

        notification.ApplyCasing(false);
        notification.Cipher = "anything";

        notification.ApplyCasing(true);

        Assert.Null(notification.Cipher);
    }

    private static AreaNotification Fading(TimeSpan fadeOutDuration) => new(
        null,
        "Summerford Farms",
        TimeSpan.FromMilliseconds(1),
        TimeSpan.Zero,
        TimeSpan.Zero,
        TimeSpan.FromMilliseconds(1),
        fadeOutDuration);

    private static bool RunUntil(AreaNotification notification, Func<AreaNotification, bool> until)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);

        while (DateTime.UtcNow < deadline)
        {
            notification.Update();

            if (until(notification))
                return true;

            Thread.Sleep(10);
        }

        return false;
    }

    [Fact]
    public void AnInterruptedFadeOutIsHalfTheNaturalOne()
    {
        Assert.Equal(
            TimeSpan.FromSeconds(1),
            AreaNotification.InterruptedFadeOutFor(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void AnInterruptedFadeOutIsNeverShortEnoughToSnap()
    {
        Assert.Equal(
            TimeSpan.FromMilliseconds(120),
            AreaNotification.InterruptedFadeOutFor(TimeSpan.FromMilliseconds(150)));
    }

    // FadeOutDuration goes down to 0.05s, below the floor, and cutting a line short must never
    // leave it on screen longer than letting it end on its own would have.
    [Fact]
    public void AnInterruptedFadeOutNeverOutlastsTheFadeItStandsInFor()
    {
        Assert.Equal(
            TimeSpan.FromMilliseconds(50),
            AreaNotification.InterruptedFadeOutFor(TimeSpan.FromMilliseconds(50)));
    }

    [Fact]
    public void BeingInterruptedStartsTheFadeImmediately()
    {
        var notification = Fading(TimeSpan.FromSeconds(1));

        notification.Dismiss();

        Assert.True(notification.IsFadingOut);
    }

    [Fact]
    public void AnInterruptedNotificationIsGoneWellInsideItsConfiguredFadeOut()
    {
        var notification = Fading(TimeSpan.FromSeconds(1));

        notification.Dismiss();
        Thread.Sleep(800);
        notification.Update();

        Assert.True(notification.IsDone);
    }

    // The other half of the same rule: shortening the interrupted fade must not have shortened
    // the ordinary one, which is a reading time the user configured.
    [Fact]
    public void ANaturalFadeOutStillTakesTheFullConfiguredTime()
    {
        var notification = Fading(TimeSpan.FromSeconds(2));

        Assert.True(RunUntil(notification, n => n.IsFadingOut));

        Thread.Sleep(1300);
        notification.Update();

        Assert.False(notification.IsDone);
    }

    [Fact]
    public void BeingPushedDownDoesNotTeleportTheLine()
    {
        var notification = Fading(TimeSpan.FromSeconds(1));

        notification.PushDown(91f);

        Assert.Equal(0f, notification.StackOffset);
    }

    [Fact]
    public void BeingPushedDownArrivesAtTheFullDistance()
    {
        var notification = Fading(TimeSpan.FromSeconds(1));

        notification.PushDown(91f);
        Assert.True(RunUntil(notification, n => n.StackOffset >= 90.5f));
    }

    [Fact]
    public void SeveralArrivalsInARowKeepPushingTheSameLineFurtherDown()
    {
        var notification = Fading(TimeSpan.FromSeconds(1));

        notification.PushDown(91f);
        Assert.True(RunUntil(notification, n => n.StackOffset >= 90.5f));

        notification.PushDown(91f);
        Assert.True(RunUntil(notification, n => n.StackOffset >= 181.5f));
    }
}
