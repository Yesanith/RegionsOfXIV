using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace RegionsOfXIV.UI.Components;

// One setting: a label on the left, its control on the right, and the help text on hover. Begin
// draws the label and leaves the cursor where the control goes; End reserves the row. Shaped this
// way rather than around a delegate so a caller can pass ref locals to the control it draws.
internal readonly struct SettingsRow
{
    private const float RowHeight = 40f;
    private const float HelpIconGap = 7f;
    private const float BlockBottomGap = 6f;
    private const float NoteTopGap = 6f;
    private const float NoteBottomGap = 8f;

    public const float ToggleHeight = ToggleSwitch.TrackHeight;

    private readonly Vector2 origin;
    private readonly float width;

    private SettingsRow(Vector2 origin, float width)
    {
        this.origin = origin;
        this.width = width;
    }

    public static SettingsRow Begin(string label, string? help, float controlWidth, float controlHeight = 0f, bool enabled = true)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var rightEdge = SettingsGroup.ContentRightEdge;
        var rowHeight = RowHeight * scale;
        var hovered = ImGui.IsMouseHoveringRect(origin, origin + new Vector2(rightEdge - origin.X, rowHeight));
        var middleY = origin.Y + (rowHeight * 0.5f);

        DrawTopDivider(origin, rightEdge);
        var labelHovered = DrawLabel(origin, middleY, label, hovered, enabled);
        var iconHovered = DrawHelpIcon(origin, middleY, label, help, hovered);

        if (!string.IsNullOrEmpty(help) && (labelHovered || iconHovered))
        {
            Tooltip.Show(help);
        }

        var resolvedHeight = controlHeight > 0f ? controlHeight * scale : ImGui.GetFrameHeight();
        ImGui.SetCursorScreenPos(new Vector2(rightEdge - (controlWidth * scale), middleY - (resolvedHeight * 0.5f)));
        return new SettingsRow(origin, rightEdge - origin.X);
    }

    public void End()
    {
        ImGui.SetCursorScreenPos(this.origin);
        ImGui.Dummy(new Vector2(this.width, RowHeight * ImGuiHelpers.GlobalScale));
    }

    // A label line on its own, for content that follows below it rather than beside it.
    public static void Block(string label, string? help) => Begin(label, help, 0f).End();

    public static void EndBlock() => Styling.VSpace(BlockBottomGap);

    public static void Note(string text, Vector4? color = null)
    {
        Styling.VSpace(NoteTopGap);
        TextDraw.Paragraph(text, color ?? Styling.TextMuted, SettingsGroup.InnerWidth());
        Styling.VSpace(NoteBottomGap);
    }

    private static void DrawTopDivider(Vector2 origin, float rightEdge)
    {
        if (SettingsGroup.RowDrawnInGroup)
        {
            ImGui.GetWindowDrawList().AddLine(origin, origin with { X = rightEdge }, ImGui.GetColorU32(Styling.Hairline), 1f);
        }

        SettingsGroup.RowDrawnInGroup = true;
    }

    private static bool DrawLabel(Vector2 origin, float middleY, string label, bool rowHovered, bool enabled)
    {
        var labelSize = ImGui.CalcTextSize(label);
        ImGui.SetCursorScreenPos(new Vector2(origin.X, middleY - (labelSize.Y * 0.5f)));
        var color = !enabled ? Styling.TextMuted : rowHovered ? Styling.TextStrong : Styling.TextSecondary;
        using (ImRaii.PushColor(ImGuiCol.Text, color))
        {
            ImGui.TextUnformatted(label);
        }

        return ImGui.IsItemHovered();
    }

    private static bool DrawHelpIcon(Vector2 origin, float middleY, string label, string? help, bool rowHovered)
    {
        if (string.IsNullOrEmpty(help) || !rowHovered)
        {
            return false;
        }

        var labelWidth = ImGui.CalcTextSize(label).X;
        var iconString = FontAwesomeIcon.InfoCircle.ToIconString();
        using (Fonts.PushIcon())
        {
            var iconSize = ImGui.CalcTextSize(iconString);
            ImGui.SetCursorScreenPos(new Vector2(origin.X + labelWidth + (HelpIconGap * ImGuiHelpers.GlobalScale), middleY - (iconSize.Y * 0.5f)));
            using (ImRaii.PushColor(ImGuiCol.Text, Styling.WithAlpha(Styling.TextMuted, 0.9f)))
            {
                ImGui.TextUnformatted(iconString);
            }
        }

        return ImGui.IsItemHovered();
    }
}
