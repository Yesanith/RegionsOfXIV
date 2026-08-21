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
}
