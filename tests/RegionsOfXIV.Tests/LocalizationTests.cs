using System.Text;
using RegionsOfXIV.Services;

namespace RegionsOfXIV.Tests;

// The whole point of the loader is that nothing it is fed can put a raw key on screen or throw
// under the draw. These are the ways a locale file goes wrong in practice.
// Anything else that reads Loc belongs in this collection too. The table is global and swapped
// about below, and xUnit runs classes in parallel by default, so a class outside it can be handed
// a language it never asked for partway through an assertion.
[Collection("Localization")]
public class LocalizationTests : IDisposable
{
    // Loc holds one table for the whole plugin, so each test puts it back to English afterwards.
    public void Dispose() => Loc.Use(null);

    private static Dictionary<string, string> Parse(string json) =>
        Loc.Parse(new MemoryStream(Encoding.UTF8.GetBytes(json)));

    private static void Apply(string json) => Loc.Apply("de", Parse(json));

    [Fact]
    public void AKeyThatIsTranslatedShowsTheTranslation()
    {
        Apply("""{ "durations.hold": { "message": "Halten" } }""");

        Assert.Equal("Halten", Loc.Get("durations.hold", "Hold"));
    }

    // Label and Unit cache what they build, so each of these asks twice: once to fill the cache
    // and once to read it. A cache that is never read back would pass a single-call test.
    [Fact]
    public void ALabelKeepsItsIdentityAndTakesTheTranslation()
    {
        Apply("""{ "motion.hold": { "message": "Halten" } }""");

        Assert.Equal("Halten###motion.hold", Loc.Label("motion.hold", "Hold"));
        Assert.Equal("Halten###motion.hold", Loc.Label("motion.hold", "Hold"));
    }

    // The one that matters. A cached label that outlived the table would leave the whole window in
    // the language it started in, with nothing on screen saying so.
    [Fact]
    public void ALabelFollowsTheTableWhenTheLanguageChanges()
    {
        Apply("""{ "motion.hold": { "message": "Halten" } }""");
        Assert.Equal("Halten###motion.hold", Loc.Label("motion.hold", "Hold"));

        Apply("""{ "motion.hold": { "message": "Maintien" } }""");

        Assert.Equal("Maintien###motion.hold", Loc.Label("motion.hold", "Hold"));
    }

    // Back to English is a table swap like any other, and the one a player reaches for when a
    // translation has gone wrong.
    [Fact]
    public void ALabelGoesBackToEnglishWhenTheTableEmpties()
    {
        Apply("""{ "motion.hold": { "message": "Halten" } }""");
        Assert.Equal("Halten###motion.hold", Loc.Label("motion.hold", "Hold"));

        Loc.Use(null);

        Assert.Equal("Hold###motion.hold", Loc.Label("motion.hold", "Hold"));
    }

    [Fact]
    public void AUnitDoublesItsPerCentSignEveryTimeItIsAsked()
    {
        Apply("""{ "units.percent": { "message": "%" } }""");

        Assert.Equal("%%", Loc.Unit("units.percent", "%"));
        Assert.Equal("%%", Loc.Unit("units.percent", "%"));
    }

    [Fact]
    public void AUnitFollowsTheTableWhenTheLanguageChanges()
    {
        Apply("""{ "units.px": { "message": "Px" } }""");
        Assert.Equal("Px", Loc.Unit("units.px", "px"));

        Apply("""{ "units.px": { "message": "pt" } }""");

        Assert.Equal("pt", Loc.Unit("units.px", "px"));
    }

    [Fact]
    public void AKeyThatIsMissingShowsTheEnglish()
    {
        Apply("""{ "durations.hold": { "message": "Halten" } }""");

        Assert.Equal("Fade in", Loc.Get("durations.fadein", "Fade in"));
    }

    // The state a translator leaves a file in half way through, and the one CheapLoc would have
    // rendered as "#durations.hold".
    [Theory]
    [InlineData("\"\"")]
    [InlineData("\"   \"")]
    [InlineData("null")]
    [InlineData("42")]
    public void AnEntryWithNothingUsableInItShowsTheEnglish(string value)
    {
        Apply($$"""{ "durations.hold": { "message": {{value}} } }""");

        Assert.Equal("Hold", Loc.Get("durations.hold", "Hold"));
    }

    [Fact]
    public void AnEntryWithNoMessageAtAllShowsTheEnglish()
    {
        Apply("""{ "durations.hold": { "description": "a note to the translator" } }""");

        Assert.Equal("Hold", Loc.Get("durations.hold", "Hold"));
    }

    [Fact]
    public void OneBrokenEntryDoesNotCostTheRestOfTheLanguage()
    {
        Apply("""
              {
                "durations.hold": "not an object",
                "durations.fadein": { "message": "Einblenden" }
              }
              """);

        Assert.Equal("Hold", Loc.Get("durations.hold", "Hold"));
        Assert.Equal("Einblenden", Loc.Get("durations.fadein", "Fade in"));
    }

    [Fact]
    public void UnderscoreKeysAreNotesRatherThanStrings()
    {
        var strings = Parse("""
                            {
                              "_status": "machine-drafted",
                              "durations.hold": { "message": "Halten" }
                            }
                            """);

        Assert.Equal(["durations.hold"], strings.Keys);
    }

    [Fact]
    public void ADescriptionIsForTheTranslatorAndIsNeverShown()
    {
        Apply("""{ "durations.hold": { "message": "Halten", "description": "Slider label." } }""");

        Assert.Equal("Halten", Loc.Get("durations.hold", "Hold"));
    }

    [Fact]
    public void AnUnknownLanguageLeavesEverythingInEnglish()
    {
        Loc.Use("pl");

        Assert.Equal("en", Loc.Current);
        Assert.Equal("Hold", Loc.Get("durations.hold", "Hold"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NoLanguageAtAllLeavesEverythingInEnglish(string? code)
    {
        Loc.Use(code);

        Assert.Equal("en", Loc.Current);
        Assert.Equal("Hold", Loc.Get("durations.hold", "Hold"));
    }

    [Fact]
    public void AnEmptyLanguageIsNotReportedAsActive()
    {
        Loc.Apply("de", []);

        Assert.Equal("en", Loc.Current);
    }

    [Fact]
    public void EnglishIsNotBundled()
    {
        // It lives at the call sites, so a file for it would be a second copy of every string.
        Assert.DoesNotContain("en", Loc.Shipped);
    }

    // This was vacuous while nothing shipped, so it asserts there is something to walk before
    // walking it -- otherwise a glob that stopped embedding the locale files would leave the test
    // passing on an empty list and say nothing.
    [Fact]
    public void EveryBundledLanguageLoads()
    {
        Assert.NotEmpty(Loc.Shipped);

        foreach (var code in Loc.Shipped)
        {
            Loc.Use(code);

            Assert.Equal(code, Loc.Current);
        }
    }

    // The window is drawn with the game's AXIS font, which has no glyph for much of Latin
    // Extended-A, Vietnamese, Hangul, Thai, Hebrew, Arabic or non-Russian Cyrillic. A locale using
    // any of those draws blanks, and Loc warns at load -- but a language this plugin ships should
    // never be the one to trip it, so it is caught here rather than in somebody's game.
    //
    // Reads the bundled files rather than the loaded table, so it checks every string in them and
    // not just the keys something happens to ask for.
    [Fact]
    public void NoBundledLanguageNeedsGlyphsTheWindowLacks()
    {
        Assert.NotEmpty(Loc.Shipped);

        foreach (var code in Loc.Shipped)
        {
            using var stream = typeof(Loc).Assembly
                .GetManifestResourceStream($"RegionsOfXIV.Localization.{code}.json");

            Assert.NotNull(stream);

            foreach (var (key, text) in Loc.Parse(stream!))
            {
                foreach (var c in text)
                    Assert.False(Loc.WindowFontLacks(c), $"{code}/{key}: U+{(int)c:X4} '{c}'");
            }
        }
    }

    // --- widget identity ---

    // ImGui hashes a widget's label to get its identity. Without the "###" the tab you were on
    // would reset when the language changed, and two labels that translated alike would collide
    // into one control that does not respond.
    [Fact]
    public void ALabelCarriesTheKeyAsItsImGuiIdentity()
    {
        Apply("""{ "durations.hold": { "message": "Halten" } }""");

        Assert.Equal("Halten###durations.hold", Loc.Label("durations.hold", "Hold"));
    }

    [Fact]
    public void ALabelKeepsTheSameIdentityInEveryLanguage()
    {
        var english = Loc.Label("durations.hold", "Hold");

        Apply("""{ "durations.hold": { "message": "Halten" } }""");
        var german = Loc.Label("durations.hold", "Hold");

        Assert.NotEqual(english, german);
        Assert.Equal(Identity(english), Identity(german));

        static string Identity(string label) => label[(label.IndexOf("###") + 3)..];
    }

    // The collision that would otherwise merge two controls: same visible word, different keys.
    [Fact]
    public void TwoControlsTranslatingAlikeKeepSeparateIdentities()
    {
        Apply("""
              {
                "durations.hold": { "message": "Dauer" },
                "durations.motion": { "message": "Dauer" }
              }
              """);

        Assert.NotEqual(
            Loc.Label("durations.hold", "Hold"),
            Loc.Label("durations.motion", "Motion"));
    }

    // --- units ---

    // A unit is concatenated onto a printf specifier and the result goes to sprintf, so a per-cent
    // sign in the translation would open a specifier with no argument behind it. Doubling turns it
    // into the literal sign the translator was asking for.
    [Theory]
    [InlineData("%", "%%")]
    [InlineData("%%", "%%%%")]
    [InlineData("%d", "%%d")]
    [InlineData("Pixel", "Pixel")]
    public void APerCentSignInAUnitIsEscaped(string translated, string expected)
    {
        Apply($$"""{ "units.px": { "message": "{{translated}}" } }""");

        Assert.Equal(expected, Loc.Unit("units.px", "px"));
    }

    [Fact]
    public void AUnitThatIsMissingShowsTheEnglish()
    {
        Apply("""{ "durations.hold": { "message": "Halten" } }""");

        Assert.Equal("px", Loc.Unit("units.px", "px"));
    }

    // --- placeholders ---

    [Fact]
    public void PlaceholdersAreFilledFromTheTranslation()
    {
        Apply("""{ "about.version": { "message": "Fassung {0}" } }""");

        Assert.Equal("Fassung 0.4.2.0", Loc.Format("about.version", "Version {0}", "0.4.2.0"));
    }

    // A translator can write "{0" or "{2}" with one argument. Neither may reach the draw as an
    // exception, so the English pattern takes over.
    [Theory]
    [InlineData("Fassung {0")]
    [InlineData("Fassung {2}")]
    public void MalformedPlaceholdersFallBackToTheEnglishPattern(string broken)
    {
        Apply($$"""{ "about.version": { "message": "{{broken}}" } }""");

        Assert.Equal("Version 0.4.2.0", Loc.Format("about.version", "Version {0}", "0.4.2.0"));
    }

    [Fact]
    public void AMissingKeyStillFormatsTheEnglish()
    {
        Apply("""{ "durations.hold": { "message": "Halten" } }""");

        Assert.Equal("Version 0.4.2.0", Loc.Format("about.version", "Version {0}", "0.4.2.0"));
    }

    // Numbers follow the language of the sentence, not the operating system. Whatever this machine
    // runs as, a German window formats German and an English one formats English.
    [Fact]
    public void NumbersAreFormattedInTheLanguageOfTheText()
    {
        Apply("""{ "general.alpha": { "message": "Zu {0:F1} % deckend." } }""");

        Assert.Equal("Zu 50,5 % deckend.", Loc.Format("general.alpha", "{0:F1}% solid.", 50.5f));
    }

    [Fact]
    public void EnglishFormatsEnglishWhateverTheMachineIsSetTo()
    {
        Loc.Use(null);

        Assert.Equal("50.5% solid.", Loc.Format("general.alpha", "{0:F1}% solid.", 50.5f));
    }

    // --- glyph coverage ---

    [Theory]
    [InlineData('a')]
    [InlineData('ß')]
    [InlineData('é')]
    [InlineData('ç')]
    [InlineData('Œ')]
    [InlineData('š')]
    [InlineData('リ')]
    [InlineData('森')]
    [InlineData('Я')]
    [InlineData('\u2014')]
    public void TheWindowDrawsWhatTheGameFontAlreadyCarried(char c)
    {
        Assert.False(Loc.WindowFontLacks(c));
    }

    // Polish, Czech, Turkish, Romanian and Vietnamese, none of which the game font carries. They
    // are drawable because UI/WindowFont merges the Windows interface font in for the extended
    // Latin blocks, and they were blanks before it.
    [Theory]
    [InlineData('ł')]
    [InlineData('ń')]
    [InlineData('ż')]
    [InlineData('č')]
    [InlineData('ř')]
    [InlineData('ı')]
    [InlineData('İ')]
    [InlineData('ğ')]
    [InlineData('Ğ')]
    [InlineData('ş')]
    [InlineData('Ş')]
    [InlineData('ț')]
    [InlineData('ệ')]
    public void TheWindowDrawsWhatTheMergeAddsForLatinLanguages(char c)
    {
        Assert.False(Loc.WindowFontLacks(c));
    }

    // The merge is Latin only, so the scripts it leaves out are still worth warning about.
    [Theory]
    [InlineData('한')]
    [InlineData('ก')]
    [InlineData('א')]
    [InlineData('ب')]
    public void TheWindowStillCannotDrawTheScriptsTheMergeLeavesOut(char c)
    {
        Assert.True(Loc.WindowFontLacks(c));
    }

    // The game font carries the Russian alphabet and nothing else Cyrillic, and the merge is Latin
    // only, so Russian is viable and its neighbours are not. Easy to get wrong in the other
    // direction and warn about all Cyrillic.
    [Theory]
    [InlineData('А')]
    [InlineData('я')]
    [InlineData('Ё')]
    [InlineData('ё')]
    public void TheWindowDrawsRussian(char c)
    {
        Assert.False(Loc.WindowFontLacks(c));
    }

    // Ukrainian and Serbian sit just outside the Russian range.
    [Theory]
    [InlineData('Є')]
    [InlineData('є')]
    [InlineData('І')]
    [InlineData('і')]
    [InlineData('Ї')]
    [InlineData('ї')]
    [InlineData('Ґ')]
    [InlineData('ґ')]
    [InlineData('Ђ')]
    [InlineData('Љ')]
    [InlineData('Џ')]
    public void TheWindowCannotDrawUkrainianOrSerbian(char c)
    {
        Assert.True(Loc.WindowFontLacks(c));
    }

    // --- language codes ---

    // A regional file used to be dropped on the floor by a length check, which quietly made the
    // README's "a new language is a file" untrue for anything but a bare two-letter code.
    [Theory]
    [InlineData("de")]
    [InlineData("pt-BR")]
    [InlineData("zh-Hans")]
    public void ARegionalCodeCountsAsALanguage(string code)
    {
        Assert.True(Loc.LooksLikeLanguageCode(code));
    }

    [Theory]
    [InlineData("e")]
    [InlineData("")]
    [InlineData("en_US")]
    [InlineData("readme")]
    public void SomethingThatIsNotALanguageCodeIsIgnored(string code)
    {
        Assert.False(Loc.LooksLikeLanguageCode(code));
    }
}
