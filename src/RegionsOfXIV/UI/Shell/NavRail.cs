using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using RegionsOfXIV.Services;
using RegionsOfXIV.UI.Components;

namespace RegionsOfXIV.UI.Shell;

internal static class NavRail
{
    private readonly record struct Entry(ConfigWindow.Page Page, FontAwesomeIcon Icon, string Id);

    private const float TopPad = 12f;
    private const float Gap = 8f;

    private static readonly Entry[] Entries =
    [
        new(ConfigWindow.Page.Announcements, FontAwesomeIcon.Bullhorn, "##rox-nav-announcements"),
        new(ConfigWindow.Page.Appearance, FontAwesomeIcon.Palette, "##rox-nav-appearance"),
        new(ConfigWindow.Page.Motion, FontAwesomeIcon.Wind, "##rox-nav-motion"),
        new(ConfigWindow.Page.Fonts, FontAwesomeIcon.Font, "##rox-nav-fonts"),
        new(ConfigWindow.Page.Sound, FontAwesomeIcon.VolumeUp, "##rox-nav-sound"),
        new(ConfigWindow.Page.Presets, FontAwesomeIcon.Swatchbook, "##rox-nav-presets"),
        new(ConfigWindow.Page.Changelog, FontAwesomeIcon.Newspaper, "##rox-nav-changelog"),
        new(ConfigWindow.Page.About, FontAwesomeIcon.InfoCircle, "##rox-nav-about"),
    ];

    // Literal call sites, so the English is where the translators' export expects to find it.
    public static string Label(ConfigWindow.Page page) => page switch
    {
        ConfigWindow.Page.Announcements => Loc.Get("announcements.tab", "Announcements"),
        ConfigWindow.Page.Appearance => Loc.Get("appearance.tab", "Appearance"),
        ConfigWindow.Page.Motion => Loc.Get("motion.tab", "Motion"),
        ConfigWindow.Page.Fonts => Loc.Get("fonts.tab", "Fonts"),
        ConfigWindow.Page.Sound => Loc.Get("sound.tab", "Sound"),
        ConfigWindow.Page.Presets => Loc.Get("presets.tab", "Presets"),
        ConfigWindow.Page.Changelog => Loc.Get("changelog.tab", "What's new"),
        _ => Loc.Get("about.tab", "About"),
    };

    public static ConfigWindow.Page? Draw(ConfigWindow.Page current, bool unseenChanges)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var button = Layout.RailButton * scale;
        var gap = Gap * scale;
        var railOrigin = ImGui.GetCursorScreenPos();
        var avail = ImGui.GetContentRegionAvail().X;
        var x = railOrigin.X + ((avail - button) * 0.5f);
        var startY = railOrigin.Y + (TopPad * scale);
        var drawList = ImGui.GetWindowDrawList();

        var selectedIndex = 0;
        for (var index = 0; index < Entries.Length; index++)
        {
            if (Entries[index].Page == current)
            {
                selectedIndex = index;
            }
        }

        var indicator = Motion.Approach(Motion.Key("##rox-rail-indicator"), selectedIndex, 16f);
        var indicatorY = startY + ((button + gap) * indicator);
        var indicatorMin = new Vector2(x, indicatorY);
        var indicatorMax = indicatorMin + new Vector2(button, button);
        Paint.Glass(drawList, indicatorMin, indicatorMax, 12f * scale, Styling.AccentGold, 0.30f);
        Paint.Fill(drawList, new Vector2(railOrigin.X, indicatorY + (button * 0.25f)), new Vector2(railOrigin.X + (3f * scale), indicatorY + (button * 0.75f)),
            Styling.AccentGold, 2f * scale);

        ConfigWindow.Page? clicked = null;

        for (var index = 0; index < Entries.Length; index++)
        {
            var entry = Entries[index];
            var y = startY + ((button + gap) * index);
            ImGui.SetCursorScreenPos(new Vector2(x, y));
            var hit = Hit.Area(entry.Id, new Vector2(button, button));
            var hover = Motion.Hover(Motion.Key(entry.Id), hit.Hovered);
            var selected = index == selectedIndex;

            if (!selected && hover > 0.01f)
            {
                Paint.Fill(drawList, new Vector2(x, y), new Vector2(x + button, y + button), Styling.WithAlpha(Styling.Surface2, 0.8f * hover), 12f * scale);
            }

            var center = new Vector2(x + (button * 0.5f), y + (button * 0.5f));
            var color = selected ? Styling.TextStrong : Vector4.Lerp(Styling.TextDim, Styling.TextSecondary, hover);
            TextDraw.IconCentered(entry.Icon, center, color);

            if (entry.Page == ConfigWindow.Page.Changelog && current != ConfigWindow.Page.Changelog && unseenChanges)
            {
                DrawBadge(drawList, center, button);
            }

            if (hit.Hovered)
            {
                Tooltip.Show(Label(entry.Page));
            }

            if (hit.Clicked)
            {
                clicked = entry.Page;
            }
        }

        ImGui.SetCursorScreenPos(railOrigin);
        ImGui.Dummy(new Vector2(avail, (TopPad * scale) + ((button + gap) * Entries.Length)));
        return clicked;
    }

    private static void DrawBadge(ImDrawListPtr drawList, Vector2 center, float button)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var badgeCenter = center + new Vector2(button * 0.30f, -button * 0.30f);
        var radius = 3.5f * scale;
        drawList.AddCircleFilled(badgeCenter, radius + (1.5f * scale), Paint.Col(Styling.WindowBg));
        drawList.AddCircleFilled(badgeCenter, radius, Paint.Col(Styling.PulseColor(Styling.AccentGold, Styling.AccentGoldSoft, Styling.PulseMedium)));
    }
}
