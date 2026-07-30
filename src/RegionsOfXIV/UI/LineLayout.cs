namespace RegionsOfXIV.UI;

// Where each glyph of one line sits, remembered between frames.
//
// Laying a line out means asking ImGui for the width of every prefix of it,
// which is quadratic in the number of glyphs and allocates a substring per
// measurement. Doing that sixty times a second for an answer that has not
// changed is the single most wasteful thing the draw path did.
//
// And it usually has not changed. A notification's text is fixed for its whole
// life; the tracking moves only when a slider does; the anchor moves only when
// the viewport or the position setting does; and the font's metrics change only
// when the atlas is rebuilt, which FontService.Generation reports. So the cache
// hits on nearly every frame, and the misses are exactly the frames where
// something the player did made the old answer wrong.
//
// One of these per line per font: the same text under the Eorzean face and under
// the display face are two different layouts, which is the whole basis of the
// decode.
internal sealed class LineLayout
{
    private string? text;
    private float tracking;
    private float centerX;
    private int generation = -1;

    private float[] positions = [];

    // Whether what is remembered still answers the question being asked.
    //
    // Reference equality on the text is deliberate and sufficient: the strings
    // come from a notification's own cached fields, so an unchanged line is
    // literally the same instance, and a changed one is a new one. Comparing
    // character by character would be a per-frame cost to detect a case that
    // cannot arise.
    public bool IsCurrent(string line, float tracking, float centerX, int generation) =>
        ReferenceEquals(this.text, line)
        && this.tracking == tracking
        && this.centerX == centerX
        && this.generation == generation;

    public float[] Positions => this.positions;

    public float[] Store(string line, float tracking, float centerX, int generation, float[] positions)
    {
        this.text = line;
        this.tracking = tracking;
        this.centerX = centerX;
        this.generation = generation;
        this.positions = positions;

        return positions;
    }
}
