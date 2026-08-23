namespace RegionsOfXIV.UI;

// Per-line memo for the two measurements a notification needs every frame.
//
// Both are keyed on the text by reference, not by value, which is only safe because
// AreaNotification.ApplyCasing hands back the same string instance until the casing setting
// actually changes. If that ever starts returning a fresh string per frame, both caches miss
// every frame and the glyph positions get recomputed at O(n^2) in the length of the line.
internal sealed class LineLayout
{
    private string? text;
    private float tracking;
    private float centerX;
    private float scale = 1f;
    private int generation = -1;

    private float[] positions = [];

    // Where each glyph goes. Depends on the scale and centre as well as the text, so this misses
    // whenever the line is repositioned or auto-shrunk to fit.
    public bool IsCurrent(string line, float tracking, float centerX, float scale, int generation) =>
        ReferenceEquals(this.text, line)
        && this.tracking == tracking
        && this.centerX == centerX
        && this.scale == scale
        && this.generation == generation;

    public float[] Positions => this.positions;

    public float Width { get; private set; }

    public void Store(
        string line, float tracking, float centerX, float scale, int generation,
        float[] positions, float width)
    {
        this.text = line;
        this.tracking = tracking;
        this.centerX = centerX;
        this.scale = scale;
        this.generation = generation;
        this.positions = positions;
        Width = width;
    }

    private string? measuredText;
    private float measuredTracking;
    private int measuredGeneration = -1;

    public float NaturalWidth { get; private set; }

    // How wide the line wants to be at scale 1. Deliberately a looser key than IsCurrent: the
    // auto-shrink has to know this before it can work out the scale, so it cannot be folded into
    // the layout above without a circular dependency.
    public bool HasNaturalWidth(string line, float tracking, int generation) =>
        ReferenceEquals(this.measuredText, line)
        && this.measuredTracking == tracking
        && this.measuredGeneration == generation;

    public void StoreNaturalWidth(string line, float tracking, int generation, float width)
    {
        this.measuredText = line;
        this.measuredTracking = tracking;
        this.measuredGeneration = generation;
        NaturalWidth = width;
    }
}
