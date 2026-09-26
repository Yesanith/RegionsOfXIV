using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using RegionsOfXIV.Services;
using RegionsOfXIV.UI.Components;

namespace RegionsOfXIV.UI.Shell;

// The strip along the top of the window: icon, name and version on the left; the editing switch,
// the preview button, Discord and close on the right. It also moves the window, since the ImGui
// title bar is not drawn.
internal static class HeaderBar
{
    private const string Title = "Regions of XIV";
    private const float PadX = 16f;
    private const float IconBox = 26f;
    private const float ButtonSize = 30f;
    private const float ButtonGap = 6f;
    private const int ButtonCount = 2;
    private const float ControlGap = 14f;
    private const float ChipPadX = 8f;
    private const float ChipPadY = 3f;
    private const float ToggleLabelGap = 8f;

    public const float MinimumWidth = 620f;

    private static readonly string VersionLabel = "v" + Changelog.Current;

    public static float ButtonsWidth() => ((ButtonSize * ButtonCount) + (ButtonGap * (ButtonCount - 1))) * ImGuiHelpers.GlobalScale;

    public static void HandleDrag(Vector2 windowPos, float width, float height)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dragWidth = width - (PadX * scale) - ButtonsWidth() - (8f * scale);
        ImGui.SetCursorScreenPos(windowPos);
        ImGui.InvisibleButton("##rox-drag", new Vector2(MathF.Max(1f, dragWidth), height));

        // The switch and the preview button sit on top of this strip, so they must be allowed to
        // take the hover and the click from it.
        ImGui.SetItemAllowOverlap();
        if (!ImGui.IsItemActive())
        {
            return;
        }

        var delta = ImGui.GetIO().MouseDelta;
        if (delta != Vector2.Zero)
        {
            ImGui.SetWindowPos(ImGui.GetWindowPos() + delta, ImGuiCond.Always);
        }
    }

    public static void Draw(ConfigWindow window, Vector2 origin, float width, float height, float windowRounding)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var end = origin + new Vector2(width, height);
        var padX = PadX * scale;
        var midY = origin.Y + (height * 0.5f);

        Paint.Fill(drawList, origin, end, Styling.WithAlpha(Styling.Surface1, 0.40f), windowRounding, ImDrawFlags.RoundCornersTop);
        Paint.Hairline(drawList, new Vector2(origin.X, end.Y - 0.5f), new Vector2(end.X, end.Y - 0.5f));

        var iconBox = IconBox * scale;
        var iconMin = new Vector2(origin.X + padX, midY - (iconBox * 0.5f));
        AppIcon.Draw(drawList, iconMin, iconMin + new Vector2(iconBox, iconBox), 7f * scale);

        var controlsLeft = DrawControls(window, end, midY);
        var x = iconMin.X + iconBox + (12f * scale);
        using (Fonts.PushHeadline())
        {
            var titleSize = TextDraw.Measure(Title);
            if (x + titleSize.X <= controlsLeft)
            {
                TextDraw.At(Title, new Vector2(x, midY - (titleSize.Y * 0.5f)), Styling.TextStrong);
                x += titleSize.X + (12f * scale);
            }
        }

        DrawVersionChip(drawList, x, controlsLeft, midY);
    }

    private static void DrawVersionChip(ImDrawListPtr drawList, float x, float rightLimit, float midY)
    {
        var scale = ImGuiHelpers.GlobalScale;
        using (Fonts.PushCaption())
        {
            var labelSize = TextDraw.Measure(VersionLabel);
            var chipMin = new Vector2(x, midY - (labelSize.Y * 0.5f) - (ChipPadY * scale));
            var chipMax = new Vector2(x + labelSize.X + (ChipPadX * 2f * scale), midY + (labelSize.Y * 0.5f) + (ChipPadY * scale));
            if (chipMax.X > rightLimit)
            {
                return;
            }

            Paint.Pill(drawList, chipMin, chipMax, Styling.WithAlpha(Styling.AccentGold, 0.12f), Styling.WithAlpha(Styling.AccentGold, 0.38f));
            TextDraw.At(VersionLabel, new Vector2(chipMin.X + (ChipPadX * scale), midY - (labelSize.Y * 0.5f)), Styling.AccentGoldSoft);
        }
    }

    // Right to left: close, Discord, the preview button, then the editing switch with its label.
    // Returns the x where the controls begin, so the title knows how much room it has.
    private static float DrawControls(ConfigWindow window, Vector2 end, float midY)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var padX = PadX * scale;
        var buttonSize = ButtonSize * scale;
        var stride = buttonSize + (ButtonGap * scale);
        var top = midY - (buttonSize * 0.5f);

        ImGui.SetCursorScreenPos(new Vector2(end.X - padX - buttonSize, top));
        if (IconButton.Draw(FontAwesomeIcon.Times, "##rox-close", buttonSize, tooltip: Loc.Get("window.close", "Close")))
        {
            window.IsOpen = false;
        }

        ImGui.SetCursorScreenPos(new Vector2(end.X - padX - buttonSize - stride, top));
        if (IconButton.Draw(FontAwesomeIcon.Comments, "##rox-discord", buttonSize, Styling.Lighten(Styling.AccentDiscord, 0.35f)))
        {
            DiscordLink.Open();
        }

        // Formatted only while hovered, so the address is not built on every frame.
        if (ImGui.IsItemHovered())
        {
            Tooltip.Show(Loc.Format("window.discord", "Join the Discord\n{0}", DiscordLink.Invite));
        }

        var x = end.X - padX - buttonSize - stride - (ControlGap * scale);

        var preview = Loc.Get("window.preview", "Preview");
        var previewWidth = PillButton.Width(preview, FontAwesomeIcon.Play);
        x -= previewWidth;
        ImGui.SetCursorScreenPos(new Vector2(x, midY - (Layout.PillHeight * scale * 0.5f)));
        if (PillButton.Draw("##rox-preview", preview, Styling.AccentGold, PillButton.Emphasis.Tinted, FontAwesomeIcon.Play,
                tooltip: Loc.Get("window.preview.tooltip", "Fires a sample notification so you can see the current settings.")))
        {
            window.FirePreview();
        }

        x -= ControlGap * scale;
        return DrawEditingSwitch(window, x, midY);
    }

    private static float DrawEditingSwitch(ConfigWindow window, float rightX, float midY)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var toggleWidth = ToggleSwitch.TrackWidth * scale;
        var toggleHeight = ToggleSwitch.TrackHeight * scale;
        var toggleX = rightX - toggleWidth;

        ImGui.SetCursorScreenPos(new Vector2(toggleX, midY - (toggleHeight * 0.5f)));
        var editing = window.Editing;
        if (ToggleSwitch.Draw("##rox-editing", ref editing))
        {
            window.SetEditing(editing);
        }

        var toggleHovered = ImGui.IsItemHovered();

        var label = Loc.Get("window.editing", "Editing mode");
        var labelSize = TextDraw.Measure(label);
        var labelX = toggleX - (ToggleLabelGap * scale) - labelSize.X;
        TextDraw.At(label, new Vector2(labelX, midY - (labelSize.Y * 0.5f)), editing ? Styling.AccentGoldSoft : Styling.TextDim);

        var labelHovered = ImGui.IsMouseHoveringRect(new Vector2(labelX, midY - (labelSize.Y * 0.5f)), new Vector2(toggleX, midY + (labelSize.Y * 0.5f)));
        if (toggleHovered || labelHovered)
        {
            Tooltip.Show(Loc.Get(
                "window.editing.tooltip",
                "Keeps one sample notification on screen while you work, instead of\n" +
                "starting a new one every time you change something.\n\n" +
                "Zone announcements are held back while this is on. It switches itself\n" +
                "off when you close this window."));
        }

        return labelX - (ControlGap * scale);
    }
}
