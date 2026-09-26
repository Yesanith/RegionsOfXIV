using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using RegionsOfXIV.Services;
using RegionsOfXIV.UI.Components;

namespace RegionsOfXIV.UI;

internal sealed partial class ConfigWindow
{
    private const float RailDotColumn = 26f;
    private const float RailDotRadius = 5f;
    private const float EntryGap = 14f;
    private const float EntryPadX = 18f;
    private const float EntryPadY = 16f;
    private const float EntrySeparatorGap = 12f;
    private const float BulletColumn = 18f;
    private const float BulletRadius = 2.5f;
    private const float BulletGap = 8f;
    private const float PillPadX = 8f;
    private const float PillPadY = 3f;
    private const float PillGap = 10f;
    private const float EntryRevealMs = 360f;
    private const float EntryStaggerMs = 80f;
    private const float EntryRevealSlide = 10f;

    // How many entries at the top of the list are new to this player. Set once after an update
    // and kept until the page has been seen, so the rail badge and the "new" pills agree.
    private int unseenChanges;

    private bool changelogAfterUpdate;

    public void ShowChangelog()
    {
        this.changelogAfterUpdate = false;
        Show(Page.Changelog);
    }

    // Opens on the changelog after an update, marking only what is new to this player. Opens
    // nothing if there is nothing to say, so a reinstall at the same version stays quiet.
    public void ShowChangelogSince(Version? lastSeen)
    {
        this.unseenChanges = Changelog.Since(lastSeen).Length;
        if (this.unseenChanges == 0)
        {
            return;
        }

        this.changelogAfterUpdate = true;
        Show(Page.Changelog);
    }

    private void DrawChangelogPage()
    {
        var subtitle = !this.changelogAfterUpdate
            ? Loc.Get("changelog.all", "Every release, newest first:")
            : this.unseenChanges == 1
                ? Loc.Get("changelog.updated", "Regions of XIV has updated. Here is what changed:")
                : Loc.Get("changelog.updated.away", "Regions of XIV has updated. Here is what changed while you were away:");

        PageHeader.Draw(Loc.Get("changelog.tab", "What's new"), subtitle);

        var entries = Changelog.All;
        for (var index = 0; index < entries.Length; index++)
        {
            var progress = Motion.Reveal(this.pageShownTick, EntryRevealMs, index * EntryStaggerMs);
            using (Motion.PushReveal(progress, EntryRevealSlide))
            {
                DrawChangelogEntry(entries[index], index, index == entries.Length - 1);
            }
        }
    }

    private void DrawChangelogEntry(in ChangelogEntry entry, int index, bool isLast)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var isNew = index < this.unseenChanges;
        var isLatest = index == 0;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var railWidth = RailDotColumn * scale;
        var cardMin = new Vector2(origin.X + railWidth, origin.Y);
        var cardWidth = width - railWidth;
        var padX = EntryPadX * scale;
        var padY = EntryPadY * scale;
        var textLeft = cardMin.X + padX + (BulletColumn * scale);
        var textWidth = MathF.Max(1f, cardMin.X + cardWidth - padX - textLeft);

        float headerHeight;
        using (Fonts.PushHeadline())
        {
            headerHeight = TextDraw.LineHeight();
        }

        var bodyHeight = MeasureChanges(entry.Changes, textWidth);
        var separatorY = cardMin.Y + padY + headerHeight + (EntrySeparatorGap * scale);
        var cardHeight = separatorY - cardMin.Y + (EntrySeparatorGap * scale) + bodyHeight + padY;
        var cardMax = new Vector2(cardMin.X + cardWidth, cardMin.Y + cardHeight);
        var drawList = ImGui.GetWindowDrawList();
        var rounding = Styling.CardRounding * scale;
        var headerMidY = cardMin.Y + padY + (headerHeight * 0.5f);

        DrawChangelogRail(drawList, origin, headerMidY, cardHeight, isNew || isLatest, isLast);

        if (isNew)
        {
            Paint.Glow(drawList, cardMin, cardMax, rounding, Styling.AccentGold, 0.8f);
        }

        if (isLatest)
        {
            Paint.Glass(drawList, cardMin, cardMax, rounding, Styling.AccentGold, 0.08f);
            Paint.Stroke(drawList, cardMin, cardMax, Styling.WithAlpha(Styling.AccentGold, isNew ? 0.55f : 0.30f), rounding);
        }
        else
        {
            Paint.Surface(drawList, cardMin, cardMax, rounding, Styling.WithAlpha(Styling.Surface1, 0.75f), Styling.WithAlpha(Styling.BorderDim, 0.5f));
        }

        var x = cardMin.X + padX;
        using (Fonts.PushHeadline())
        {
            var version = VersionLabel(index);
            TextDraw.At(version, new Vector2(x, cardMin.Y + padY), isLatest ? Styling.AccentGoldSoft : Styling.TextStrong);
            x += TextDraw.Measure(version).X + (PillGap * scale);
        }

        if (isNew || isLatest)
        {
            DrawChangelogPill(drawList, isNew ? Loc.Get("changelog.new", "New") : Loc.Get("changelog.latest", "Latest"), x, headerMidY, isNew);
        }

        Paint.Hairline(drawList, new Vector2(cardMin.X + padX, separatorY), new Vector2(cardMax.X - padX, separatorY));
        DrawChanges(drawList, entry.Changes, cardMin.X + padX, textLeft, textWidth, separatorY + (EntrySeparatorGap * scale));

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardHeight + (isLast ? 0f : EntryGap * scale)));
    }

    private static readonly string[] VersionLabels = new string[Changelog.All.Length];

    private static string VersionLabel(int index)
        => VersionLabels[index] ??= Changelog.All[index].Version.ToString();

    private static void DrawChangelogRail(ImDrawListPtr drawList, Vector2 origin, float dotY, float cardHeight, bool highlighted, bool isLast)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var center = new Vector2(origin.X + (RailDotColumn * scale * 0.5f) - (2f * scale), dotY);
        var radius = RailDotRadius * scale;
        if (!isLast)
        {
            var lineBottom = origin.Y + cardHeight + (EntryGap * scale) + (dotY - origin.Y) - radius;
            drawList.AddLine(center + new Vector2(0f, radius + (2f * scale)), new Vector2(center.X, lineBottom),
                Paint.Col(Styling.WithAlpha(Styling.BorderDim, 0.8f)), 1.5f * scale);
        }

        if (highlighted)
        {
            Paint.Dot(drawList, center, radius, Styling.AccentGold, 0.28f);
            return;
        }

        drawList.AddCircle(center, radius, Paint.Col(Styling.WithAlpha(Styling.TextDim, 0.8f)), 0, 1.5f * scale);
    }

    private static void DrawChangelogPill(ImDrawListPtr drawList, string label, float x, float midY, bool filled)
    {
        var scale = ImGuiHelpers.GlobalScale;
        using (Fonts.PushCaption())
        {
            var text = TextDraw.Upper(label);
            var size = TextDraw.Measure(text);
            var min = new Vector2(x, midY - (size.Y * 0.5f) - (PillPadY * scale));
            var max = new Vector2(x + size.X + (PillPadX * 2f * scale), midY + (size.Y * 0.5f) + (PillPadY * scale));
            if (filled)
            {
                Paint.Pill(drawList, min, max, Styling.AccentGold, Styling.WithAlpha(Styling.AccentGoldSoft, 0.6f));
                TextDraw.At(text, new Vector2(min.X + (PillPadX * scale), midY - (size.Y * 0.5f)), Styling.ForegroundOn(Styling.AccentGold));
                return;
            }

            Paint.Pill(drawList, min, max, Styling.WithAlpha(Styling.AccentGold, 0.16f), Styling.WithAlpha(Styling.AccentGold, 0.4f));
            TextDraw.At(text, new Vector2(min.X + (PillPadX * scale), midY - (size.Y * 0.5f)), Styling.AccentGoldSoft);
        }
    }

    private static float MeasureChanges(string[] changes, float textWidth)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var height = 0f;
        for (var index = 0; index < changes.Length; index++)
        {
            if (index > 0)
            {
                height += BulletGap * scale;
            }

            height += TextDraw.MeasureWrapped(changes[index], textWidth).Y;
        }

        return height;
    }

    // Release notes stay English, and they are drawn unformatted all the same: a per-cent sign in
    // one would otherwise be read as a format specifier.
    private static void DrawChanges(ImDrawListPtr drawList, string[] changes, float bulletX, float textLeft, float textWidth, float top)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var lineHeight = TextDraw.LineHeight();
        var y = top;
        for (var index = 0; index < changes.Length; index++)
        {
            if (index > 0)
            {
                y += BulletGap * scale;
            }

            var text = changes[index];
            drawList.AddCircleFilled(new Vector2(bulletX + (BulletRadius * scale), y + (lineHeight * 0.5f)), BulletRadius * scale, Paint.Col(Styling.AccentGold));
            TextDraw.Wrapped(text, new Vector2(textLeft, y), textWidth, Styling.TextSecondary);
            y += TextDraw.MeasureWrapped(text, textWidth).Y;
        }
    }
}
