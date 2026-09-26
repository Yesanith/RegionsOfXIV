using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace RegionsOfXIV.UI.Components;

// A titled card that grows around the rows drawn inside it. The card is painted after the rows,
// on a lower draw channel, so its height is whatever the content turned out to need.
internal sealed class SettingsGroup : IDisposable
{
    private const float PaddingX = 14f;
    private const float PaddingY = 8f;
    private const float GroupGap = 18f;
    private const float TitleGap = 6f;
    private const float CaptionGap = 8f;

    internal static float ContentRightEdge { get; private set; }

    internal static bool RowDrawnInGroup;

    private readonly Vector2 cardOrigin;
    private readonly float cardWidth;

    public static SettingsGroup Begin(string title, string? caption = null)
    {
        if (title.Length > 0)
        {
            Styling.SectionLabel(title);
        }

        if (!string.IsNullOrEmpty(caption))
        {
            using (Fonts.PushCaption())
            {
                TextDraw.Paragraph(caption, Styling.TextDim);
            }

            Styling.VSpace(CaptionGap);
        }
        else if (title.Length > 0)
        {
            Styling.VSpace(TitleGap);
        }

        return new SettingsGroup();
    }

    private SettingsGroup()
    {
        var scale = ImGuiHelpers.GlobalScale;
        this.cardOrigin = ImGui.GetCursorScreenPos();
        this.cardWidth = ImGui.GetContentRegionAvail().X;
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

    // Local-coordinate X of the card's inner right edge, so block content can right-align to the
    // card border instead of the wider window content region.
    public static float InnerRightLocalX()
        => ImGui.GetCursorPosX() + (ContentRightEdge - ImGui.GetCursorScreenPos().X);

    public static float InnerWidth() => ContentRightEdge - ImGui.GetCursorScreenPos().X;
}
