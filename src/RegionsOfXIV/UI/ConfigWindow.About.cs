using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Utility;
using RegionsOfXIV.Services;
using RegionsOfXIV.UI.Components;

namespace RegionsOfXIV.UI;

internal sealed partial class ConfigWindow
{
    private const string Name = "Regions of XIV";

    private const string Author = "Yesanith";

    private const string InterfaceAuthor = "XeldarAlz";

    private const string RepositoryUrl = "https://github.com/Yesanith/RegionsOfXIV";

    private const string IssuesUrl = RepositoryUrl + "/issues";

    private const string InterfaceAuthorUrl = "https://github.com/XeldarAlz";

    private const string InspirationUrl = "https://blishhud.com/modules/?module=Nekres.Regions_Of_Tyria";

    private const float SectionGap = 22f;
    private const float RevealMs = 460f;
    private const float RevealStaggerMs = 110f;
    private const float RevealSlide = 14f;

    private const float HeroMinHeight = 172f;
    private const float HeroPad = 26f;
    private const float HeroIconSize = 100f;
    private const float HeroTextGap = 26f;
    private const float HeroLineGap = 6f;
    private const float ChipPadX = 10f;
    private const float ChipPadY = 4f;
    private const float ChipIconGap = 6f;
    private const float ChipGap = 8f;

    private const float TileHeight = 96f;
    private const float TileGap = 14f;
    private const float TileStackBelow = 720f;
    private const float TilePad = 18f;
    private const float TileMedallion = 26f;
    private const float TileTextGap = 16f;
    private const float TileLift = 3f;
    private const float ArrowSlide = 5f;

    private static readonly Vector2[] BloomOffsets = [new(1.6f, 0f), new(-1.6f, 0f), new(0f, 1.6f), new(0f, -1.6f)];

    private static readonly string VersionText = Changelog.Current.ToString();

    private readonly record struct CommunityLink(string Id, FontAwesomeIcon Icon, string Url, Vector4 Accent);

    private static readonly CommunityLink[] CommunityLinks =
    [
        new("##rox-about-discord", FontAwesomeIcon.Comments, DiscordInvite, Styling.AccentDiscord),
        new("##rox-about-github", FontAwesomeIcon.CodeBranch, RepositoryUrl, Styling.AccentGold),
        new("##rox-about-issue", FontAwesomeIcon.Bug, IssuesUrl, Styling.AccentRose),
    ];

    // Literal call sites, so the English is where the translators' export expects to find it.
    private static string CommunityTitle(int index) => index switch
    {
        0 => Loc.Get("about.discord", "Join the Discord"),
        1 => Loc.Get("about.github", "GitHub"),
        _ => Loc.Get("about.issue", "Report an issue"),
    };

    private static string CommunityBody(int index) => index switch
    {
        0 => Loc.Get("about.discord.tooltip", "Ideas, bug reports and preset codes."),
        1 => Loc.Get("about.github.tooltip", "The source, the releases, and the licence."),
        _ => Loc.Get("about.issue.tooltip", "Something wrong, or something missing."),
    };

    private static string Summary => Loc.Get(
        "about.summary",
        "Announces the region, zone, area and sub-area you walk into, and the weather "
        + "while you are there, replacing the game's own location text rather than "
        + "drawing alongside it.");

    private float aboutColumnX;
    private float aboutColumnWidth;

    // Bound once, so drawing the page does not allocate a delegate per section per frame.
    private Action[]? aboutSections;

    private string? authorChip;
    private string? pluginCredit;
    private string? interfaceCredit;
    private int creditsGeneration = -1;

    private void RefreshCredits()
    {
        if (this.authorChip is not null && this.creditsGeneration == Loc.Generation)
        {
            return;
        }

        this.creditsGeneration = Loc.Generation;
        this.authorChip = Loc.Format("about.author", "by {0}", Author);
        this.pluginCredit = Loc.Format("about.credit.plugin", "Plugin developed by {0}", Author);
        this.interfaceCredit = Loc.Format("about.credit.interface", "User Interface designed and developed by {0}", InterfaceAuthor);
    }

    private void DrawAboutPage()
    {
        this.aboutColumnWidth = ImGui.GetContentRegionAvail().X;
        this.aboutColumnX = ImGui.GetCursorScreenPos().X;
        RefreshCredits();

        this.aboutSections ??= [DrawHero, DrawCommunity, DrawCommands, DrawCredits, DrawAboutFooter];

        for (var index = 0; index < this.aboutSections.Length; index++)
        {
            if (index > 0)
            {
                Styling.VSpace(SectionGap);
            }

            RevealSection(index, this.aboutSections[index]);
        }
    }

    private void RevealSection(int index, Action draw)
    {
        var elapsed = Environment.TickCount64 - this.pageShownTick;
        var progress = Motion.Reduced ? 1f : Motion.EaseOutCubic(Math.Clamp((elapsed - (index * RevealStaggerMs)) / RevealMs, 0f, 1f));
        var cursorY = ImGui.GetCursorScreenPos().Y + ((1f - progress) * RevealSlide * ImGuiHelpers.GlobalScale);
        ImGui.SetCursorScreenPos(new Vector2(this.aboutColumnX, cursorY));
        using (Motion.PushAlpha(progress))
        {
            draw();
        }
    }

    private void DrawHero()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = this.aboutColumnWidth;
        var pad = HeroPad * scale;
        var iconSize = HeroIconSize * scale;
        var textGap = HeroTextGap * scale;
        var textWidth = MathF.Min(HeroTextWidth(), MathF.Max(1f, width - (pad * 2f) - iconSize - textGap));
        var textHeight = HeroTextHeight(textWidth);
        var height = MathF.Max(HeroMinHeight * scale, textHeight + (pad * 2f));
        var max = origin + new Vector2(width, height);
        var rounding = Styling.CardRounding * 1.6f * scale;

        Paint.Shadow(drawList, origin, max, rounding, 16f * scale, 0.5f);
        Paint.Gradient(drawList, origin, max, Styling.Tint(Styling.Surface2, Styling.AccentGold, 0.18f), Styling.Tint(Styling.Surface0, Styling.AccentGold, 0.05f), rounding);
        DrawAurora(drawList, origin, max);
        Paint.TopLight(drawList, origin, max, rounding, 0.14f);
        Paint.Stroke(drawList, origin, max, Styling.WithAlpha(Styling.AccentGold, 0.35f), rounding, 1.2f * scale);

        var groupX = origin.X + ((width - iconSize - textGap - textWidth) * 0.5f);
        var bob = Motion.Reduced ? 0f : Motion.Wave(3200) * 3f * scale;
        var iconCenter = new Vector2(groupX + (iconSize * 0.5f), origin.Y + (height * 0.5f) + bob);
        DrawHeroIcon(drawList, iconCenter, iconSize);
        DrawHeroText(groupX + iconSize + textGap, textWidth, origin.Y + ((height - textHeight) * 0.5f));

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private static void DrawAurora(ImDrawListPtr drawList, Vector2 min, Vector2 max)
    {
        var width = max.X - min.X;
        var height = max.Y - min.Y;
        drawList.PushClipRect(min, max, true);
        SoftBlob(drawList, min + new Vector2(width * (0.18f + (0.08f * Motion.Wave(11000))), height * (0.30f + (0.20f * Motion.Wave(13700)))), height * 1.3f, Styling.AccentGold, 0.10f);
        SoftBlob(drawList, min + new Vector2(width * (0.72f + (0.10f * Motion.Wave(15500))), height * (0.10f + (0.25f * Motion.Wave(9300)))), height * 1.1f, Styling.AccentEmber, 0.08f);
        SoftBlob(drawList, min + new Vector2(width * (0.95f + (0.05f * Motion.Wave(17900))), height * (0.95f + (0.10f * Motion.Wave(12100)))), height * 1.2f, Styling.AccentBlue, 0.06f);
        drawList.PopClipRect();
    }

    // Many faint rings stacked together fall off smoothly; a handful of stronger ones shows as
    // visible bands.
    private static void SoftBlob(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 color, float peak)
    {
        const int layers = 18;
        var layerAlpha = peak * 2f / layers;
        for (var layer = layers; layer >= 1; layer--)
        {
            var fraction = layer / (float)layers;
            var alpha = (layerAlpha * (1f - Motion.Smoothstep(fraction))) + (layerAlpha * 0.15f);
            drawList.AddCircleFilled(center, radius * fraction, Paint.Col(Styling.WithAlpha(color, alpha)), 64);
        }
    }

    private static void DrawHeroIcon(ImDrawListPtr drawList, Vector2 center, float size)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var half = size * 0.5f;
        ProgressRing.Glow(center, half * 1.05f, Styling.AccentGold, 0.4f + (0.35f * Styling.Pulse(Styling.PulseBreath)));

        var iconMin = center - new Vector2(half, half);
        var iconMax = center + new Vector2(half, half);
        var rounding = size * 0.22f;
        AppIcon.Draw(drawList, iconMin, iconMax, rounding);
        Paint.Stroke(drawList, iconMin, iconMax, Styling.WithAlpha(Styling.AccentGoldSoft, 0.55f), rounding, 1.5f * scale);
    }

    // The wider of the title and the row of chips; the summary wraps to whatever this comes to.
    private float HeroTextWidth()
    {
        float width;
        using (Fonts.PushTitle())
        {
            width = TextDraw.Measure(Name).X;
        }

        using (Fonts.PushCaption())
        {
            var scale = ImGuiHelpers.GlobalScale;
            return MathF.Max(width,
                ChipWidth(FontAwesomeIcon.Tag, VersionText)
                + (ChipGap * scale) + ChipWidth(FontAwesomeIcon.Code, this.authorChip!)
                + (ChipGap * scale) + ChipWidth(FontAwesomeIcon.Newspaper, Loc.Get("about.whatsnew", "What's new")));
        }
    }

    private static float HeroTextHeight(float width)
    {
        var scale = ImGuiHelpers.GlobalScale;
        float titleHeight;
        using (Fonts.PushTitle())
        {
            titleHeight = TextDraw.LineHeight();
        }

        var summaryHeight = TextDraw.MeasureWrapped(Summary, width).Y;
        float chipHeight;
        using (Fonts.PushCaption())
        {
            chipHeight = TextDraw.LineHeight() + (ChipPadY * 2f * scale);
        }

        return titleHeight + (HeroLineGap * scale) + summaryHeight + (HeroLineGap * 2f * scale) + chipHeight;
    }

    private void DrawHeroText(float x, float width, float y)
    {
        var scale = ImGuiHelpers.GlobalScale;
        float titleHeight;
        using (Fonts.PushTitle())
        {
            titleHeight = TextDraw.LineHeight();
        }

        DrawShimmerTitle(Name, new Vector2(x, y), width);
        y += titleHeight + (HeroLineGap * scale);

        var summary = Summary;
        TextDraw.Wrapped(summary, new Vector2(x, y), width, Styling.TextSecondary);
        y += TextDraw.MeasureWrapped(summary, width).Y + (HeroLineGap * 2f * scale);

        using (Fonts.PushCaption())
        {
            var chipHeight = TextDraw.LineHeight() + (ChipPadY * 2f * scale);
            var chipX = x;
            chipX += StaticChip(FontAwesomeIcon.Tag, VersionText, Styling.AccentGold, new Vector2(chipX, y), chipHeight) + (ChipGap * scale);
            chipX += StaticChip(FontAwesomeIcon.Code, this.authorChip!, Styling.AccentBlue, new Vector2(chipX, y), chipHeight) + (ChipGap * scale);
            if (LinkChip("##rox-about-whatsnew", FontAwesomeIcon.Newspaper, Loc.Get("about.whatsnew", "What's new"), Styling.AccentGoldSoft, new Vector2(chipX, y), chipHeight,
                    Loc.Get("about.whatsnew.tooltip", "Every release, newest first. Also at \"/regions changelog\".")))
            {
                ShowChangelog();
            }
        }
    }

    private static void DrawShimmerTitle(string text, Vector2 position, float width)
    {
        using var font = Fonts.PushTitle();
        var size = TextDraw.Measure(text);
        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(position, position + new Vector2(width, size.Y), true);
        var bloom = Styling.WithAlpha(Styling.AccentGold, 0.22f);
        for (var index = 0; index < BloomOffsets.Length; index++)
        {
            TextDraw.At(text, position + (BloomOffsets[index] * ImGuiHelpers.GlobalScale), bloom);
        }

        TextDraw.At(text, position, Styling.TextStrong);
        var bandWidth = size.X * 0.4f;
        var bandCenter = position.X - bandWidth + (Styling.Phase(Styling.PulseOrbit) * (size.X + (bandWidth * 2f)));
        drawList.PushClipRect(new Vector2(bandCenter - (bandWidth * 0.5f), position.Y), new Vector2(bandCenter + (bandWidth * 0.5f), position.Y + size.Y), true);
        TextDraw.At(text, position, Styling.AccentGoldSoft);
        drawList.PopClipRect();
        drawList.PopClipRect();
    }

    private static float ChipWidth(FontAwesomeIcon icon, string label)
    {
        var scale = ImGuiHelpers.GlobalScale;
        return (ChipPadX * 2f * scale) + TextDraw.IconSize(icon).X + (ChipIconGap * scale) + TextDraw.Measure(label).X;
    }

    private static float StaticChip(FontAwesomeIcon icon, string label, Vector4 accent, Vector2 origin, float height)
    {
        var width = ChipWidth(icon, label);
        var max = origin + new Vector2(width, height);
        Paint.Pill(ImGui.GetWindowDrawList(), origin, max, Styling.WithAlpha(accent, 0.12f), Styling.WithAlpha(accent, 0.38f));
        DrawChipContent(icon, label, accent, Styling.TextSecondary, origin, height);
        return width;
    }

    // A null tooltip leaves the hover to the caller, for one that is formatted and so only worth
    // building while hovered.
    private static bool LinkChip(string id, FontAwesomeIcon icon, string label, Vector4 accent, Vector2 origin, float height, string? tooltip)
    {
        var width = ChipWidth(icon, label);
        var size = new Vector2(width, height);
        ImGui.SetCursorScreenPos(origin);
        var hit = Hit.Area(id, size);
        var hover = Motion.Hover(Motion.Key(id), hit.Hovered);
        var max = origin + size;
        Paint.Pill(ImGui.GetWindowDrawList(), origin, max, Styling.WithAlpha(accent, 0.14f + (0.14f * hover)), Styling.WithAlpha(accent, 0.45f + (0.35f * hover)));
        DrawChipContent(icon, label, accent, Vector4.Lerp(Styling.TextSecondary, Styling.TextStrong, hover), origin, height);
        if (hit.Hovered && tooltip is not null)
        {
            Tooltip.Show(tooltip);
        }

        return hit.Clicked;
    }

    private static void DrawChipContent(FontAwesomeIcon icon, string label, Vector4 iconColor, Vector4 textColor, Vector2 origin, float height)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var midY = origin.Y + (height * 0.5f);
        var iconSize = TextDraw.IconSize(icon);
        var x = origin.X + (ChipPadX * scale);
        TextDraw.Icon(icon, new Vector2(x, midY - (iconSize.Y * 0.5f)), iconColor);
        x += iconSize.X + (ChipIconGap * scale);
        TextDraw.At(label, new Vector2(x, midY - (TextDraw.LineHeight() * 0.5f)), textColor);
    }

    private void DrawCommunity()
    {
        var scale = ImGuiHelpers.GlobalScale;
        SectionHeader(FontAwesomeIcon.Users, Loc.Get("about.community", "Community"), Styling.AccentDiscord, this.aboutColumnWidth);

        var origin = new Vector2(this.aboutColumnX, ImGui.GetCursorScreenPos().Y);
        var gap = TileGap * scale;
        var columns = this.aboutColumnWidth < TileStackBelow * scale ? 1 : CommunityLinks.Length;
        var tileSize = new Vector2((this.aboutColumnWidth - (gap * (columns - 1))) / columns, TileHeight * scale);
        for (var index = 0; index < CommunityLinks.Length; index++)
        {
            var column = index % columns;
            var row = index / columns;
            var offset = new Vector2(column * (tileSize.X + gap), row * (tileSize.Y + gap));
            DrawTile(CommunityLinks[index], CommunityTitle(index), CommunityBody(index), origin + offset, tileSize);
        }

        var rows = (CommunityLinks.Length + columns - 1) / columns;
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(this.aboutColumnWidth, (rows * tileSize.Y) + ((rows - 1) * gap)));
    }

    private static void DrawTile(in CommunityLink link, string title, string body, Vector2 slot, Vector2 size)
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.SetCursorScreenPos(slot);
        var hit = Hit.Area(link.Id, size);
        var hover = Motion.Hover(Motion.Key(link.Id), hit.Hovered);
        var press = Motion.Approach(Motion.Key(link.Id, 1), hit.Held ? 1f : 0f, 30f);
        OpenOrCopy(hit, link.Url, body);

        var lift = ((TileLift * hover) - (press * 1.5f)) * scale;
        var min = slot - new Vector2(0f, lift);
        var max = min + size;
        var rounding = Styling.CardRounding * 1.4f * scale;
        var drawList = ImGui.GetWindowDrawList();
        var accent = link.Accent;

        if (hover > 0.01f)
        {
            Paint.Shadow(drawList, min, max, rounding, 12f * scale, 0.35f * hover);
            Paint.Glow(drawList, min, max, rounding, accent, hover);
        }

        Paint.Glass(drawList, min, max, rounding, accent, 0.09f + (0.10f * hover));
        Paint.Stroke(drawList, min, max, Styling.WithAlpha(accent, 0.28f + (0.5f * hover)), rounding, 1.2f * scale);

        var pad = TilePad * scale;
        var radius = TileMedallion * scale;
        var medallionCenter = new Vector2(min.X + pad + radius, min.Y + (size.Y * 0.5f));
        drawList.AddCircleFilled(medallionCenter, radius * (1.25f + (0.1f * hover)), Paint.Col(Styling.WithAlpha(accent, 0.10f + (0.10f * hover))), 40);
        drawList.AddCircleFilled(medallionCenter, radius, Paint.Col(Vector4.Lerp(accent, Styling.Lighten(accent, 0.15f), hover)), 40);
        drawList.AddCircle(medallionCenter, radius, Paint.Col(Styling.WithAlpha(Styling.Lighten(accent, 0.5f), 0.55f)), 40, 1.2f * scale);
        TextDraw.IconCentered(link.Icon, medallionCenter, Styling.ForegroundOn(accent), 1.1f + (0.12f * hover));

        var arrowSize = TextDraw.IconSize(FontAwesomeIcon.ArrowRight);
        var arrowX = max.X - pad - arrowSize.X - (ArrowSlide * scale * (1f - hover));
        TextDraw.Icon(FontAwesomeIcon.ArrowRight, new Vector2(arrowX, min.Y + ((size.Y - arrowSize.Y) * 0.5f)),
            Vector4.Lerp(Styling.WithAlpha(Styling.TextDim, 0.7f), Styling.Lighten(accent, 0.35f), hover));

        var textX = medallionCenter.X + radius + (TileTextGap * scale);
        var textWidth = MathF.Max(1f, arrowX - (TileTextGap * scale) - textX);
        float titleHeight;
        using (Fonts.PushHeadline())
        {
            titleHeight = TextDraw.LineHeight();
        }

        float bodyHeight;
        using (Fonts.PushCaption())
        {
            bodyHeight = TextDraw.MeasureWrapped(body, textWidth).Y;
        }

        var textY = min.Y + ((size.Y - titleHeight - (4f * scale) - bodyHeight) * 0.5f);
        using (Fonts.PushHeadline())
        {
            TextDraw.At(TextDraw.Truncate(title, textWidth), new Vector2(textX, textY), Styling.TextStrong);
        }

        using (Fonts.PushCaption())
        {
            TextDraw.Wrapped(body, new Vector2(textX, textY + titleHeight + (4f * scale)), textWidth, Vector4.Lerp(Styling.TextDim, Styling.TextSecondary, hover));
        }
    }

    private void DrawCommands()
    {
        SectionHeader(FontAwesomeIcon.Terminal, Loc.Get("about.commands", "Commands"), Styling.AccentGold, this.aboutColumnWidth);

        using var group = SettingsGroup.Begin(string.Empty);
        CommandRow("/regions", Loc.Get("about.command.settings", "Opens these settings."));
        CommandRow("/regions test", Loc.Get("about.command.test", "Shows a notification for where you are standing."));
        CommandRow("/regions changelog", Loc.Get("about.command.changelog", "Opens the What's New window."));
    }

    // The command is the label and the description is the control: a sentence right-aligned in
    // the row, truncated rather than wrapped so the row keeps its height.
    private static void CommandRow(string command, string what)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var room = SettingsGroup.InnerWidth() - TextDraw.Measure(command).X - (24f * scale);
        var text = TextDraw.Truncate(what, room);
        var width = TextDraw.Measure(text).X / scale;
        var row = SettingsRow.Begin(command, null, width, TextDraw.LineHeight() / scale);
        TextDraw.At(text, ImGui.GetCursorScreenPos(), Styling.TextDim);
        row.End();
    }

    // Who made what. The plugin is Yesanith's; the window it is set up in was made and designed
    // by XeldarAlz, and each name links to where the rest of their work lives.
    private void DrawCredits()
    {
        SectionHeader(FontAwesomeIcon.PenNib, Loc.Get("about.credits", "Credits"), Styling.AccentEmber, this.aboutColumnWidth);

        using var group = SettingsGroup.Begin(string.Empty);
        CreditRow("##rox-credit-plugin", this.pluginCredit!, Author, RepositoryUrl, Styling.AccentGold);
        CreditRow("##rox-credit-interface", this.interfaceCredit!, InterfaceAuthor, InterfaceAuthorUrl, Styling.AccentEmber);
    }

    private static void CreditRow(string id, string label, string name, string url, Vector4 accent)
    {
        var scale = ImGuiHelpers.GlobalScale;
        float chipHeight;
        float chipWidth;
        using (Fonts.PushCaption())
        {
            chipHeight = TextDraw.LineHeight() + (ChipPadY * 2f * scale);
            chipWidth = ChipWidth(FontAwesomeIcon.CodeBranch, name);
        }

        var row = SettingsRow.Begin(label, null, chipWidth / scale, chipHeight / scale);
        using (Fonts.PushCaption())
        {
            if (LinkChip(id, FontAwesomeIcon.CodeBranch, name, accent, ImGui.GetCursorScreenPos(), chipHeight, null))
            {
                Util.OpenLink(url);
            }
        }

        if (ImGui.IsItemHovered())
        {
            Tooltip.Show(Loc.Format("common.opens", "{0}\n\nOpens {1} in your browser.", name, url));
        }

        row.End();
    }

    private void DrawAboutFooter()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = this.aboutColumnWidth;

        var inspiration = Loc.Get("about.inspiration", "Inspired by Nekres' Regions of Tyria for Guild Wars 2.");
        var origin = ImGui.GetCursorScreenPos();
        var size = TextDraw.MeasureWrapped(inspiration, width);
        var hit = Hit.Area("##rox-about-inspiration", new Vector2(width, size.Y));
        var hover = Motion.Hover(Motion.Key("##rox-about-inspiration"), hit.Hovered);
        TextDraw.Wrapped(inspiration, origin, width, Vector4.Lerp(Styling.TextSecondary, Styling.AccentGoldSoft, hover));
        if (hit.Hovered)
        {
            Tooltip.Show(Loc.Format("about.inspiration.tooltip", "Click to open {0}", InspirationUrl));
        }

        if (hit.Clicked)
        {
            Util.OpenLink(InspirationUrl);
        }

        Styling.VSpace(6f);
        using (Fonts.PushCaption())
        {
            TextDraw.Paragraph(Loc.Get("about.licence", "Licensed under AGPL-3.0-or-later."), Styling.TextMuted, width);
            Styling.VSpace(2f);
            TextDraw.Paragraph(
                Loc.Get(
                    "about.notaffiliated",
                    "Place names, weather and fonts come from the game's own data. "
                    + "Not affiliated with Square Enix."),
                Styling.TextMuted, width);
        }

        Styling.VSpace(4f * scale);
    }

    private static void SectionHeader(FontAwesomeIcon icon, string label, Vector4 accent, float width)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var iconSize = TextDraw.IconSize(icon);
        var labelSize = TextDraw.SmallCapsSize(label);
        var midY = origin.Y + (iconSize.Y * 0.5f);
        TextDraw.Icon(icon, origin, accent);
        var labelX = origin.X + iconSize.X + (8f * scale);
        TextDraw.SmallCaps(label, new Vector2(labelX, midY - (labelSize.Y * 0.5f)), Styling.TextDim);
        var lineStart = labelX + labelSize.X + (12f * scale);
        Paint.GradientH(ImGui.GetWindowDrawList(), new Vector2(lineStart, midY), new Vector2(origin.X + width, midY + 1f),
            Styling.WithAlpha(accent, 0.5f), Styling.WithAlpha(accent, 0f), 0f);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, iconSize.Y));
        Styling.VSpace(8f);
    }

    private static void OpenOrCopy(Hit.Result hit, string url, string tooltip)
    {
        if (!hit.Hovered)
        {
            return;
        }

        Tooltip.Show(Loc.Format("common.opens", "{0}\n\nOpens {1} in your browser.", tooltip, url));

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            ImGui.SetClipboardText(url);
        }

        if (hit.Clicked)
        {
            Util.OpenLink(url);
        }
    }
}
