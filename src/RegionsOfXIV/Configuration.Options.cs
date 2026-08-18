namespace RegionsOfXIV;

// The choices Configuration stores as enums.
//
// One rule governs all three: they are append-only. The numeric value is what
// lands in the saved config, so a new member goes on the end and an existing one
// never moves. Reordering silently reassigns every stored choice.

// The game's fonts are bitmap atlases baked at fixed sizes rather than vector
// outlines, so any size above a family's largest native size is an upscale and
// visibly softens. Each game face therefore comes with a ceiling:
//
//   TrumpGothic  ~91 px   the narrow title face. Latin only.
//   Jupiter      ~61 px   the serif face. Latin only.
//   Axis          48 px   the general UI face, and the only game family carrying
//                         Japanese glyphs. Lowest ceiling of the three.
//   NotoSansCjk    none   shipped with Dalamud, and vector rather than bitmap, so
//                         it is crisp at any size and covers every language.
//
// NotoSansCjk is last despite being the default for new configs: it was added
// most recently, and the append-only rule outranks putting the default first.
public enum DisplayFontChoice
{
    TrumpGothic,
    Jupiter,
    Axis,
    NotoSansCjk,
}

// How the glyphs move as the text arrives.
//
// A separate axis from the decode, not a replacement for it. The decode is a
// substitution — Eorzean forms resolving into readable ones — and a motion is a
// displacement, so the two compose: letters can rise into place while they
// resolve.
public enum MotionEffect
{
    None,
    Typewriter,
    Rise,
    Wave,
    Burn,
}

// Ambient particles, drawn around the text for as long as it is on screen. A
// separate axis from MotionEffect deliberately: embers under a burn is the
// obvious pairing, but nothing stops hearts under a decode, and keeping the two
// orthogonal is both less code and more combinations.
//
// Every one of these is drawn from primitives — circles, triangles, quads —
// rather than from a glyph or a sprite. A "♥" character would render as a blank
// box under Trump Gothic and Jupiter, which are Latin-only, and a sprite sheet
// would be the first art asset this plugin has ever needed.
public enum ParticleEffect
{
    None,
    Hearts,
    Embers,
    Sparkles,
    Petals,
}
