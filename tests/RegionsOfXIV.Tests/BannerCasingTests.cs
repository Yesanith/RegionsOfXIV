using RegionsOfXIV.Services;

namespace RegionsOfXIV.Tests;

// Banners are always drawn uppercase, and until the banner language became a choice that upcasing
// was invariant. Turkish is the first wording where that is wrong: invariant has no upper case for
// a dotless i at all, so "Tamamlandı" kept a lower case letter on the end of a line of capitals,
// and every "i" lost its dot.
//
// Sets BannerNameResolver.Language directly rather than through a config, because that is what
// decides the casing and it is the thing under test.
public class BannerCasingTests : IDisposable
{
    private readonly FakeSink sink = new();

    public void Dispose() => BannerNameResolver.Language = null;

    private string Push(string language, string text)
    {
        BannerNameResolver.Language = language;
        BannerNotification.PushTo(this.sink, text);

        return this.sink.Last!.Text;
    }

    // The line from the report: "TESLIMAT TAMAMLANDı", with a dotless capital in the first word
    // and an unchanged lower case letter at the end of the second.
    [Fact]
    public void TurkishKeepsTheDotOnAnUpperCaseI()
    {
        Assert.Equal("TESLİMAT TAMAMLANDI", Push("tr", "Teslimat Tamamlandı"));
    }

    [Theory]
    [InlineData("Etkinlik Başladı", "ETKİNLİK BAŞLADI")]
    [InlineData("İleri!", "İLERİ!")]
    [InlineData("Seviye Atladı!", "SEVİYE ATLADI!")]
    public void TurkishWordingCasesByTurkishRules(string wording, string expected)
    {
        Assert.Equal(expected, Push("tr", wording));
    }

    // The other half of the same rule: English wording must not pick up dotted capitals just
    // because some other language needs them.
    [Theory]
    [InlineData("Delivery Complete", "DELIVERY COMPLETE")]
    [InlineData("Duty Commenced", "DUTY COMMENCED")]
    public void EnglishWordingIsUnaffected(string wording, string expected)
    {
        Assert.Equal(expected, Push("en", wording));
    }

    // Casing an already uppercase line again has to be a no-op, because AreaNotification upcases
    // a second time when the Uppercase setting is on.
    [Fact]
    public void CasingTwiceChangesNothing()
    {
        var once = Push("tr", "Teslimat Tamamlandı");

        Assert.Equal(once, once.ToUpperInvariant());
    }
}
