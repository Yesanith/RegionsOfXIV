using System;

namespace RegionsOfXIV.UI;

// Stand-in glyphs for the decode effect, before a line resolves into readable text.
//
// The substitution is deterministic on the character and its position, so the scramble is stable
// from frame to frame instead of flickering. Characters the Eorzean font can actually draw are
// left alone; anything past its coverage would render as a blank box, so those are mapped onto
// plain Latin letters that it does have.
internal static class EorzeanCipher
{
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

    private const char EorzeanCoverageEnd = 'ɏ';

    private const string CipherAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    private static bool IsCoveredByEorzeanFont(char c) => c <= EorzeanCoverageEnd;
}
