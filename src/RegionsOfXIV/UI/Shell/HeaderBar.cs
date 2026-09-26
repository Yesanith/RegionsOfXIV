using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using RegionsOfXIV.Services;
using RegionsOfXIV.UI.Components;

namespace RegionsOfXIV.UI.Shell;

// The strip along the top of the window: icon, name and version on the left; the language
// picker, Discord and close on the right. It also moves the window, since the ImGui title bar is
// not drawn.
//
// The language picker lives here, findable by position, so somebody who has landed in a language
// they cannot read can still find their way out without reading anything.
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
    private const float LanguageWidth = 150f;
    private const float GlobeGap = 8f;

    public const float MinimumWidth = 700f;

    private static readonly string VersionLabel = "v" + Changelog.Current;

    public static float ButtonsWidth() => ((ButtonSize * ButtonCount) + (ButtonGap * (ButtonCount - 1))) * ImGuiHelpers.GlobalScale;

    public static void HandleDrag(Vector2 windowPos, float width, float height)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dragWidth = width - (PadX * scale) - ButtonsWidth() - (8f * scale);
        ImGui.SetCursorScreenPos(windowPos);
        ImGui.InvisibleButton("##rox-drag", new Vector2(MathF.Max(1f, dragWidth), height));

        // The language picker sits on top of this strip, so it must be allowed to take the hover
        // and the click from it.
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

    // Right to left: close, Discord, then the language picker with a globe beside it. Returns the
    // x where the controls begin, so the title knows how much room it has.
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

        var pickerWidth = LanguageWidth * scale;
        var pickerX = end.X - padX - buttonSize - stride - (ControlGap * scale) - pickerWidth;
        ImGui.SetCursorScreenPos(new Vector2(pickerX, midY - (ImGui.GetFrameHeight() * 0.5f)));
        window.DrawLanguagePicker(LanguageWidth);

        var globeSize = TextDraw.IconSize(FontAwesomeIcon.Globe);
        var globeX = pickerX - (GlobeGap * scale) - globeSize.X;
        TextDraw.Icon(FontAwesomeIcon.Globe, new Vector2(globeX, midY - (globeSize.Y * 0.5f)), Styling.TextDim);

        return globeX - (ControlGap * scale);
    }
}
