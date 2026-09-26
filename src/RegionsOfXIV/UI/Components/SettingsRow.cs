using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace RegionsOfXIV.UI.Components;

// One setting: a label on the left, its control on the right, and the help text on hover. Begin
// draws the label and leaves the cursor where the control goes; End reserves the row. Shaped this
// way rather than around a delegate so a caller can pass ref locals to the control it draws.
//
// A row with help shows a small mark beside its label at all times, dim until hovered, so it is
// plain which settings have more to say.
internal readonly struct SettingsRow
{
    private const float RowHeight = 40f;
    private const float HelpIconGap = 7f;
    private const float LabelControlGap = 14f;
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
        var controlLeft = rightEdge - (controlWidth * scale);
        var hasHelp = !string.IsNullOrEmpty(help);

        DrawTopDivider(origin, rightEdge);

        var iconWidth = hasHelp ? TextDraw.IconSize(FontAwesomeIcon.InfoCircle).X + (HelpIconGap * scale) : 0f;
        var labelRoom = MathF.Max(1f, controlLeft - (LabelControlGap * scale) - iconWidth - origin.X);
        var labelHovered = DrawLabel(origin, middleY, TextDraw.Truncate(label, labelRoom), hovered, enabled);
        var iconHovered = hasHelp && DrawHelpIcon(middleY, hovered);

        if (hasHelp && (labelHovered || iconHovered))
        {
            Tooltip.Show(help!);
        }

        var resolvedHeight = controlHeight > 0f ? controlHeight * scale : ImGui.GetFrameHeight();
        ImGui.SetCursorScreenPos(new Vector2(controlLeft, middleY - (resolvedHeight * 0.5f)));
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

    // Placed straight after the label the caller just drew, so it follows a truncated label too.
    private static bool DrawHelpIcon(float middleY, bool rowHovered)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var iconString = FontAwesomeIcon.InfoCircle.ToIconString();
        using (Fonts.PushIcon())
        {
            var iconSize = ImGui.CalcTextSize(iconString);
            ImGui.SetCursorScreenPos(new Vector2(ImGui.GetItemRectMax().X + (HelpIconGap * scale), middleY - (iconSize.Y * 0.5f)));
            using (ImRaii.PushColor(ImGuiCol.Text, Styling.WithAlpha(rowHovered ? Styling.AccentGoldSoft : Styling.TextMuted, rowHovered ? 0.95f : 0.55f)))
            {
                ImGui.TextUnformatted(iconString);
            }
        }

        return ImGui.IsItemHovered();
    }
}
