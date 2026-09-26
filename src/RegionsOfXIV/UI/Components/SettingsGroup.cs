using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace RegionsOfXIV.UI.Components;

// A titled card that grows around the rows drawn inside it. The card is painted after the rows,
// on a lower draw channel, so its height is whatever the content turned out to need.
//
// ColumnWidth lets a page lay groups out side by side: while it is set, a card takes that width
// rather than the whole content region.
internal sealed class SettingsGroup : IDisposable
{
    private const float PaddingX = 14f;
    private const float PaddingY = 8f;
    private const float GroupGap = 18f;
    private const float TitleGap = 6f;
    private const float CaptionGap = 8f;

    internal static float ContentRightEdge { get; private set; }

    internal static float ColumnWidth;

    internal static bool RowDrawnInGroup;

    private readonly Vector2 cardOrigin;
    private readonly float cardWidth;

    public static float AvailableWidth()
        => ColumnWidth > 0f ? ColumnWidth : ImGui.GetContentRegionAvail().X;

    public static SettingsGroup Begin(string title, string? caption = null)
    {
        var width = AvailableWidth();

        if (title.Length > 0)
        {
            Styling.SectionLabel(title);
        }

        if (!string.IsNullOrEmpty(caption))
        {
            using (Fonts.PushCaption())
            {
                TextDraw.Paragraph(caption, Styling.TextDim, width);
            }

            Styling.VSpace(CaptionGap);
        }
        else if (title.Length > 0)
        {
            Styling.VSpace(TitleGap);
        }

        return new SettingsGroup(width);
    }

    private SettingsGroup(float width)
    {
        var scale = ImGuiHelpers.GlobalScale;
        this.cardOrigin = ImGui.GetCursorScreenPos();
        this.cardWidth = width;
        ContentRightEdge = this.cardOrigin.X + this.cardWidth - (PaddingX * scale);
        RowDrawnInGroup = false;

        var drawList = ImGui.GetWindowDrawList();
        drawList.ChannelsSplit(2);
        drawList.ChannelsSetCurrent(1);

        ImGui.SetCursorScreenPos(this.cardOrigin + (new Vector2(PaddingX, PaddingY) * scale));
        ImGui.BeginGroup();
    }

    public void Dispose()
    {
        ImGui.EndGroup();
        var scale = ImGuiHelpers.GlobalScale;
        var cardEnd = new Vector2(this.cardOrigin.X + this.cardWidth, ImGui.GetItemRectMax().Y + (PaddingY * scale));

        var drawList = ImGui.GetWindowDrawList();
        drawList.ChannelsSetCurrent(0);
        var rounding = Styling.CardRounding * scale;
        Paint.Surface(drawList, this.cardOrigin, cardEnd, rounding, Styling.WithAlpha(Styling.Surface1, 0.55f), Styling.WithAlpha(Styling.BorderDim, 0.5f));
        drawList.ChannelsMerge();

        ImGui.SetCursorScreenPos(new Vector2(this.cardOrigin.X, cardEnd.Y));
        ImGui.Dummy(new Vector2(this.cardWidth, 0f));
        Styling.VSpace(GroupGap);
    }

    public static float InnerWidth() => ContentRightEdge - ImGui.GetCursorScreenPos().X;
}
