using RegionsOfXIV.Services;
using RegionsOfXIV.UI;

namespace RegionsOfXIV.Tests;

// The cipher decides what the decode effect shows before a line resolves, and anything the bundled
// Eorzean face cannot draw appears as a blank.
//
// That face was measured by rasterising it and reading the glyph boxes, not by asking whether a
// glyph exists: every accented letter in Latin-1 is in its character map and every one of them is
// empty. What draws is ASCII, some Latin-1 symbols, and the dotless i. These pin the cipher to
// that, because the bug they cover was an assumption about a code point range nobody had checked.
public class EorzeanCipherTests
{
    private static bool DrawsInTheEorzeanFace(char c) => c is <= '~' or 'ı';

    private static void AssertEveryCharacterDraws(string text)
    {
        foreach (var c in EorzeanCipher.Build(text))
            Assert.True(DrawsInTheEorzeanFace(c), $"U+{(int)c:X4} from {text}");
    }

    [Fact]
    public void EveryTurkishBannerScramblesToSomethingThatDraws()
    {
        foreach (var name in BannerNames.Turkish.Values)
            AssertEveryCharacterDraws(name);
    }

    [Fact]
    public void EveryEnglishBannerScramblesToSomethingThatDraws()
    {
        foreach (var name in BannerNames.English.Values)
            AssertEveryCharacterDraws(name);
    }

    // German and French place names are full of these, and they drew as blanks for as long as the
    // decode effect has existed. They are not a Turkish problem.
    [Theory]
    [InlineData("Grüße aus Ul'dah")]
    [InlineData("Forêt de Chasse")]
    [InlineData("Östliches Loch")]
    public void AccentedPlaceNamesScrambleToSomethingThatDraws(string name)
    {
        AssertEveryCharacterDraws(name);
    }

    [Theory]
    [InlineData('ä', 'a')]
    [InlineData('ö', 'o')]
    [InlineData('ü', 'u')]
    [InlineData('é', 'e')]
    [InlineData('ç', 'c')]
    [InlineData('Ç', 'C')]
    [InlineData('Ü', 'U')]
    [InlineData('ğ', 'g')]
    [InlineData('Ğ', 'G')]
    [InlineData('ş', 's')]
    [InlineData('Ş', 'S')]
    [InlineData('İ', 'I')]
    public void AnAccentedLetterFoldsOntoTheLetterItIsBuiltFrom(char accented, char expected)
    {
        Assert.Equal(expected.ToString(), EorzeanCipher.Build(accented.ToString()));
    }

    // Not a base letter plus a mark, so decomposition cannot reach it and it is named instead.
    [Theory]
    [InlineData('ß', 's')]
    [InlineData('ø', 'o')]
    [InlineData('œ', 'o')]
    [InlineData('ł', 'l')]
    public void ALetterThatDoesNotDecomposeIsStillFolded(char letter, char expected)
    {
        Assert.Equal(expected.ToString(), EorzeanCipher.Build(letter.ToString()));
    }

    // Present in the face already, so it is left alone rather than folded onto a dotted i.
    [Fact]
    public void TheDotlessIIsCarriedByTheFaceAndSurvives()
    {
        Assert.Equal("ı", EorzeanCipher.Build("ı"));
    }

    [Fact]
    public void PlainAsciiIsPassedThroughUntouched()
    {
        const string text = "Duty Commenced";

        Assert.Equal(text, EorzeanCipher.Build(text));
    }

    // The scramble is drawn every frame while a line resolves, so an unstable one would flicker
    // rather than read as script.
    [Fact]
    public void TheSameTextAlwaysScramblesTheSameWay()
    {
        Assert.Equal(EorzeanCipher.Build("Etkinlik Başladı"), EorzeanCipher.Build("Etkinlik Başladı"));
    }

    [Fact]
    public void TheLineKeepsItsLength()
    {
        const string text = "FATE'e Katılındı (Bonus)";

        Assert.Equal(text.Length, EorzeanCipher.Build(text).Length);
    }
}
