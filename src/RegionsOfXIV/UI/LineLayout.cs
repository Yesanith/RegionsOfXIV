namespace RegionsOfXIV.UI;

internal sealed class LineLayout
{
    private string? text;
    private float tracking;
    private float centerX;
    private int generation = -1;

    private float[] positions = [];

    public bool IsCurrent(string line, float tracking, float centerX, int generation) =>
        ReferenceEquals(this.text, line)
        && this.tracking == tracking
        && this.centerX == centerX
        && this.generation == generation;

    public float[] Positions => this.positions;

    public float Width { get; private set; }

    public void Store(string line, float tracking, float centerX, int generation, float[] positions, float width)
    {
        this.text = line;
        this.tracking = tracking;
        this.centerX = centerX;
        this.generation = generation;
        this.positions = positions;
        Width = width;
    }
}
