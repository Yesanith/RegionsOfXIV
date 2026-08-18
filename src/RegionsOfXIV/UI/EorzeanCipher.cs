using System;

namespace RegionsOfXIV.UI;

// The unreadable half of the decode: for a given line, what to draw in its place
// while it has not resolved yet.
//
// Its own file because it is a fact about the bundled font's coverage rather than
// anything to do with how a notification is drawn, and because the reasoning
// below is longer than the code.
internal static class EorzeanCipher
{
    // What the Eorzean half draws: the text itself wherever the font covers it, and
    // a stand-in letter wherever it does not. Same length as the input, so the two
    // layouts stay index-aligned and a glyph's scrambled and resolved forms occupy
    // the same slot.
    //
    // The substitution is derived from the character and its position rather than
    // from a random source, so it is stable frame to frame. A fresh draw would make
    // the undecoded half flicker, which reads as a rendering fault rather than as
    // an effect.
    public static string Build(string text)
    {
        Span<char> cipher = text.Length <= 128 ? stackalloc char[text.Length] : new char[text.Length];

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            cipher[i] = IsCoveredByEorzeanFont(c)
                ? c
                : CipherAlphabet[((c * 31) + i) % CipherAlphabet.Length];
        }

        return new string(cipher);
    }

    // U+024F, the last codepoint of Latin Extended-B, and the end of the bundled
    // Eorzean font's coverage. Its 240 glyphs run from ASCII through the accented
    // Latin forms and stop there — no CJK, no Cyrillic, no Greek. Anything past
    // this point has no Eorzean form and needs a stand-in during the reveal.
    private const char EorzeanCoverageEnd = 'ɏ';

    // Letters the stand-in is drawn from. Uppercase only: the Eorzean uppercase
    // forms are the more ornate ones, and they are closer in width to a full-width
    // Japanese glyph than the lowercase, which keeps the line from having to grow
    // too far as it decodes.
    private const string CipherAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    private static bool IsCoveredByEorzeanFont(char c) => c <= EorzeanCoverageEnd;
}
