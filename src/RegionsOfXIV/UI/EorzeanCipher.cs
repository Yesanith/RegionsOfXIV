using System;
using System.Text;

namespace RegionsOfXIV.UI;

// Stand-in glyphs for the decode effect, before a line resolves into readable text.
//
// The substitution is deterministic on the character and its position, so the scramble is stable
// from frame to frame instead of flickering. Characters the Eorzean face can actually draw are
// left alone; anything past its coverage is folded onto the letter it is built from, and anything
// with no such letter gets a plain Latin one instead.
//
// Only the scramble is affected. The text that lands when the decode finishes is the real string,
// so nothing a player reads loses a diacritic to this.
internal static class EorzeanCipher
{
    public static string Build(string text)
    {
        Span<char> cipher = text.Length <= 128 ? stackalloc char[text.Length] : new char[text.Length];

        for (var i = 0; i < text.Length; i++)
        {
            var c = Fold(text[i]);
            cipher[i] = IsCoveredByEorzeanFont(c)
                ? c
                : CipherAlphabet[((c * 31) + i) % CipherAlphabet.Length];
        }

        return new string(cipher);
    }

    private const string CipherAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    // Measured off the bundled Eorzea.ttf by rasterising it and reading the glyph boxes, which is
    // not the same as asking whether a glyph exists. Every accented letter in Latin-1 is present
    // in the face's character map and every one of them is empty, so testing for existence says
    // they are fine and they draw as nothing.
    //
    // What actually draws is ASCII, a scattering of Latin-1 symbols, and the dotless i. The test
    // this replaced accepted everything below U+0250, so a German or French line lost every umlaut
    // and accent to a blank for as long as the decode effect has existed, and a Turkish one lost
    // most of its letters.
    private static bool IsCoveredByEorzeanFont(char c) => c is <= '~' or 'ı';

    // Folded by decomposition rather than by a table of letters, so this covers every Latin
    // diacritic at once instead of only the ones a shipped language happens to use today. The
    // decomposed form of "ğ" begins with "g", of "İ" with "I", of "ä" with "a".
    //
    // The handful that do not decompose are letters in their own right rather than a base plus a
    // mark, so they are named here.
    private static char Fold(char c)
    {
        if (IsCoveredByEorzeanFont(c))
            return c;

        var named = c switch
        {
            'ß' => 's',
            'æ' => 'a',
            'Æ' => 'A',
            'œ' => 'o',
            'Œ' => 'O',
            'ø' => 'o',
            'Ø' => 'O',
            'ð' => 'd',
            'Ð' => 'D',
            'þ' => 'p',
            'Þ' => 'P',
            'đ' => 'd',
            'Đ' => 'D',
            'ł' => 'l',
            'Ł' => 'L',
            _ => '\0',
        };

        if (named != '\0')
            return named;

        var decomposed = c.ToString().Normalize(NormalizationForm.FormD);

        return IsCoveredByEorzeanFont(decomposed[0]) ? decomposed[0] : c;
    }
}
