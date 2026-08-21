namespace RegionsOfXIV.UI;

internal sealed class LineLayout
{
    private string? text;
    private float tracking;
    private float centerX;
    private float scale = 1f;
    private int generation = -1;

    private float[] positions = [];

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
