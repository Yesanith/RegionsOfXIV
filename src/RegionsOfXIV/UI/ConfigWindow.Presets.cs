using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using RegionsOfXIV.Services;
using RegionsOfXIV.UI.Components;

namespace RegionsOfXIV.UI;

internal sealed partial class ConfigWindow
{
    private const float CardGap = 12f;
    private const float CardPadX = 16f;
    private const float CardPadY = 12f;
    private const float CardSwatch = 10f;
    private const float CardLift = 2f;

    private string newPresetName = string.Empty;

    // The stage paints whichever card is hovered, through a scratch configuration the preset is
    // applied to. Keyed by the card so a hover that stays on one card applies it once.
    private readonly Configuration presetScratch = new();

    private string? hoveredPreset;

    private string? hoveredThisFrame;

    // The colour a preset card wears: what it paints on screen, taken from the preset itself so
    // the card previews the look rather than guessing at it. Built on first use rather than at
    // type load: applying a preset repairs faint colours, and that can log.
    private static Vector4[]? CachedBuiltInAccents;

    private static Vector4[] BuiltInAccents => CachedBuiltInAccents ??= BuildBuiltInAccents();

    private static Vector4[] BuildBuiltInAccents()
    {
        var accents = new Vector4[Presets.All.Length];
        var scratch = new Configuration();
        for (var index = 0; index < Presets.All.Length; index++)
        {
            Presets.All[index].ApplyTo(scratch);
            accents[index] = AccentOf(scratch);
        }

        return accents;
    }

    private static Vector4 AccentOf(Configuration settings)
        => settings.Particles != ParticleEffect.None ? settings.ParticleColor with { W = 1f } : settings.TextColor with { W = 1f };

    // Applying a preset rewrites the whole config, so afterwards the fonts have to be rebuilt and
    // the native UI suppression re-checked -- the preset may have turned either of those off.
    private void DrawPresetsPage()
    {
        PageHeader.Draw(
            Loc.Get("presets.tab", "Presets"),
            Loc.Get(
                "presets.intro",
                "A preset covers Announcements, Appearance, Motion and Fonts: the face and size of "
                + "each line, position, colours, motion and particles, which tiers announce, the "
                + "timings, and when to stay quiet. Sound is not included."));

        using (Fonts.PushCaption())
        {
            TextDraw.Paragraph(
                Loc.Get("presets.intro.warning", "Applying one replaces all of them, so anything you have changed is overwritten."),
                Styling.AccentAmberSoft);
        }

        Styling.VSpace(14f);

        var applied = false;
        this.hoveredThisFrame = null;

        SectionHeading(
            Loc.Get("presets.builtin", "Built-in looks"),
            Loc.Get("presets.builtin.hint", "The defaults, plus the look each one is named for."));
        applied |= DrawBuiltInPresets();

        Styling.VSpace(18f);
        SectionHeading(
            Loc.Get("presets.saved", "Your presets"),
            Loc.Get("presets.saved.hint", "Your settings, saved exactly as they were."));

        if (this.config.UserPresets.Count == 0)
        {
            using (Fonts.PushCaption())
            {
                TextDraw.Paragraph(Loc.Get("presets.saved.none", "You have not saved any yet."), Styling.TextMuted);
            }
        }

        applied |= DrawSavedPresets();

        Styling.VSpace(18f);
        DrawCustomFontPresetWarning();
        DrawSavePresetRow();

        Styling.VSpace(18f);
        applied |= DrawShareCodeRow();

        Styling.VSpace(10f);
        if (PillButton.Draw("##rox-presets-discord", Loc.Get("presets.discord", "Join the Discord"), Styling.AccentDiscord,
                PillButton.Emphasis.Tinted, FontAwesomeIcon.Comments))
        {
            DiscordLink.Open();
        }

        if (ImGui.IsItemHovered())
        {
            Tooltip.Show(Loc.Format(
                "common.opens", "{0}\n\nOpens {1} in your browser.",
                Loc.Get("presets.discord.tooltip", "Trade preset codes, report a bug, or suggest a feature."),
                DiscordInvite));
        }

        SettleHoverPreview();

        if (!applied)
        {
            return;
        }

        this.actions.RebuildFonts();

        if (!this.config.HideNativeAreaText)
        {
            this.actions.RestoreNativeAreaText();
        }

        if (!this.config.HideNativeLoadingTitle)
        {
            this.actions.RestoreNativeLoadingTitle();
        }

        this.config.Save();
        this.stage.Replay();
        this.actions.Preview(Sample);
    }

    private static void SectionHeading(string title, string hint)
    {
        Styling.SectionLabel(title);
        using (Fonts.PushCaption())
        {
            TextDraw.Paragraph(hint, Styling.TextDim);
        }

        Styling.VSpace(8f);
    }

    // Built-in preset names are identifiers, not prose: they travel inside share codes, so a
    // "Inferno" saved here has to arrive as "Inferno" on someone else's machine. Only the
    // description is translated.
    private static string DescriptionOf(in Preset preset) => preset.Name switch
    {
        "Classic" => Loc.Get("presets.classic.description", preset.Description),
        "Inferno" => Loc.Get("presets.inferno.description", preset.Description),
        "Sweetheart" => Loc.Get("presets.sweetheart.description", preset.Description),
        "Starlight" => Loc.Get("presets.starlight.description", preset.Description),
        "Sakura" => Loc.Get("presets.sakura.description", preset.Description),
        "Dispatch" => Loc.Get("presets.dispatch.description", preset.Description),
        "Tyria" => Loc.Get("presets.tyria.description", preset.Description),
        _ => preset.Description,
    };

    private readonly record struct CardGrid(Vector2 Origin, float CardWidth, int Columns, float Gap)
    {
        public static CardGrid Measure(int count)
        {
            var scale = ImGuiHelpers.GlobalScale;
            var available = ImGui.GetContentRegionAvail().X;
            var gap = CardGap * scale;
            var columns = Math.Clamp((int)MathF.Floor((available + gap) / ((Layout.PresetCardMinWidth * scale) + gap)), 1, Math.Max(1, count));
            var cardWidth = (available - (gap * (columns - 1))) / columns;
            return new CardGrid(ImGui.GetCursorScreenPos(), cardWidth, columns, gap);
        }

        public Vector2 Slot(int index, float height)
        {
            var column = index % this.Columns;
            var row = index / this.Columns;
            return this.Origin + new Vector2(column * (this.CardWidth + this.Gap), row * (height + this.Gap));
        }

        public void Reserve(int count, float height)
        {
            var rows = (count + this.Columns - 1) / this.Columns;
            ImGui.SetCursorScreenPos(this.Origin);
            ImGui.Dummy(new Vector2((this.CardWidth * this.Columns) + (this.Gap * (this.Columns - 1)), (rows * height) + ((rows - 1) * this.Gap)));
        }
    }

    private bool DrawBuiltInPresets()
    {
        var applied = false;
        var scale = ImGuiHelpers.GlobalScale;
        var height = Layout.PresetCardHeight * scale;
        var grid = CardGrid.Measure(Presets.All.Length);

        for (var index = 0; index < Presets.All.Length; index++)
        {
            ref readonly var preset = ref Presets.All[index];
            var hit = DrawPresetCard($"##rox-preset-{preset.Name}", preset.Name, DescriptionOf(preset), BuiltInAccents[index], grid.Slot(index, height), new Vector2(grid.CardWidth, height));

            if (hit.Hovered)
            {
                Tooltip.Show(Loc.Format("presets.builtin.tooltip", "{0}\nEverything else returns to its default.", DescriptionOf(preset)));
                HoverPreview(preset.Name, preset);
            }

            if (hit.Clicked)
            {
                preset.ApplyTo(this.config);
                applied = true;
            }
        }

        grid.Reserve(Presets.All.Length, height);
        return applied;
    }

    private bool DrawSavedPresets()
    {
        if (this.config.UserPresets.Count == 0)
        {
            return false;
        }

        var applied = false;
        var scale = ImGuiHelpers.GlobalScale;
        var height = Layout.PresetCardHeight * scale;
        var grid = CardGrid.Measure(this.config.UserPresets.Count);

        UserPreset? remove = null;
        UserPreset? overwrite = null;
        UserPreset? share = null;

        for (var index = 0; index < this.config.UserPresets.Count; index++)
        {
            var preset = this.config.UserPresets[index];
            var id = $"##rox-saved-{preset.Name}";
            var slot = grid.Slot(index, height);
            var size = new Vector2(grid.CardWidth, height);
            var hit = DrawPresetCard(id, preset.Name, Loc.Get("presets.saved.hint", "Your settings, saved exactly as they were."), AccentOf(preset.Settings), slot, size);

            if (hit.Hovered)
            {
                Tooltip.Show(Loc.Get("presets.saved.tooltip", "Apply this preset.\nRight-click to share, overwrite or delete it."));
                HoverPreview(id, preset);
            }

            if (hit.Clicked)
            {
                preset.ApplyTo(this.config);
                applied = true;
            }

            var menuId = string.Concat(id, "-menu");
            if (hit.Hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            {
                ImGui.OpenPopup(menuId);
            }

            var buttonSize = 26f * scale;
            ImGui.SetCursorScreenPos(new Vector2(slot.X + size.X - buttonSize - (CardPadX * scale * 0.5f), slot.Y + (CardPadY * scale * 0.5f)));
            if (IconButton.Draw(FontAwesomeIcon.EllipsisH, string.Concat(id, "-more"), buttonSize, Styling.TextDim))
            {
                ImGui.OpenPopup(menuId);
            }

            using (var menu = ContextMenu.Begin(menuId))
            {
                if (menu.Open)
                {
                    if (ContextMenu.Item(Loc.Get("presets.menu.share", "Copy share code")))
                    {
                        share = preset;
                    }

                    if (ContextMenu.Item(Loc.Get("presets.menu.overwrite", "Overwrite with current settings")))
                    {
                        overwrite = preset;
                    }

                    if (ContextMenu.Item(Loc.Get("presets.menu.delete", "Delete")))
                    {
                        remove = preset;
                    }
                }
            }
        }

        grid.Reserve(this.config.UserPresets.Count, height);

        if (share is not null)
        {
            ImGui.SetClipboardText(PresetCode.Encode(share.Name, share.Settings));
            Report(Loc.Format("presets.copied", "Copied a code for \"{0}\".", share.Name), failed: false);
        }

        if (overwrite is not null)
        {
            var at = this.config.UserPresets.IndexOf(overwrite);
            this.config.UserPresets[at] = UserPreset.Capture(overwrite.Name, this.config);
            this.config.Save();
        }

        if (remove is not null)
        {
            this.config.UserPresets.Remove(remove);
            this.config.Save();
        }

        return applied;
    }

    private void HoverPreview(string key, in Preset preset)
    {
        this.hoveredThisFrame = key;
        if (this.hoveredPreset == key)
        {
            return;
        }

        this.hoveredPreset = key;
        preset.ApplyTo(this.presetScratch);
        this.stage.Override(this.presetScratch);
        this.stage.Replay();
    }

    private void HoverPreview(string key, UserPreset preset)
    {
        this.hoveredThisFrame = key;
        if (this.hoveredPreset == key)
        {
            return;
        }

        this.hoveredPreset = key;
        preset.ApplyTo(this.presetScratch);
        this.stage.Override(this.presetScratch);
        this.stage.Replay();
    }

    // Back to the live look on the first frame nothing is hovered, and replayed so the change of
    // look is not a jump between two half-finished animations.
    private void SettleHoverPreview()
    {
        if (this.hoveredThisFrame is not null || this.hoveredPreset is null)
        {
            return;
        }

        this.hoveredPreset = null;
        this.stage.Override(null);
        this.stage.Replay();
    }

    private static Hit.Result DrawPresetCard(string id, string name, string description, Vector4 accent, Vector2 slot, Vector2 size)
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.SetCursorScreenPos(slot);
        var hit = Hit.Area(id, size, allowOverlap: true);
        var hover = Motion.Hover(Motion.Key(id), hit.Hovered);
        var press = Motion.Approach(Motion.Key(id, 1), hit.Held ? 1f : 0f, 30f);

        var lift = ((CardLift * hover) - press) * scale;
        var min = slot - new Vector2(0f, lift);
        var max = min + size;
        var rounding = Styling.CardRounding * scale;
        var drawList = ImGui.GetWindowDrawList();

        if (hover > 0.01f)
        {
            Paint.Glow(drawList, min, max, rounding, accent, hover * 0.8f);
        }

        Paint.Glass(drawList, min, max, rounding, accent, 0.06f + (0.08f * hover), hover);

        var padX = CardPadX * scale;
        var padY = CardPadY * scale;
        var swatch = CardSwatch * scale;
        var swatchMin = new Vector2(min.X + padX, min.Y + padY + (2f * scale));
        drawList.AddCircleFilled(swatchMin + new Vector2(swatch * 0.5f, swatch * 0.5f), swatch * 0.9f, Paint.Col(Styling.WithAlpha(accent, 0.25f)));
        drawList.AddCircleFilled(swatchMin + new Vector2(swatch * 0.5f, swatch * 0.5f), swatch * 0.5f, Paint.Col(accent));

        var textX = min.X + padX + swatch + (10f * scale);
        var textWidth = MathF.Max(1f, max.X - padX - (30f * scale) - textX);
        float titleHeight;
        using (Fonts.PushHeadline())
        {
            titleHeight = TextDraw.LineHeight();
            TextDraw.At(TextDraw.Truncate(name, textWidth), new Vector2(textX, min.Y + padY), Vector4.Lerp(Styling.TextStrong, Styling.Lighten(accent, 0.3f), hover * 0.6f));
        }

        using (Fonts.PushCaption())
        {
            drawList.PushClipRect(min, max - new Vector2(0f, padY * 0.5f), true);
            TextDraw.Wrapped(description, new Vector2(textX, min.Y + padY + titleHeight + (4f * scale)), max.X - padX - textX, Vector4.Lerp(Styling.TextDim, Styling.TextSecondary, hover));
            drawList.PopClipRect();
        }

        return hit;
    }

    private void DrawSavePresetRow()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var name = this.newPresetName.Trim();

        var existing = this.config.UserPresets.FirstOrDefault(
            p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

        var save = existing is null
            ? Loc.Get("presets.save", "Save")
            : Loc.Get("presets.replace", "Replace");
        var hint = Loc.Get("presets.name.hint", "Name it to save, copy or import");

        var buttonWidth = PillButton.Width(save, FontAwesomeIcon.Save);
        var fieldWidth = Math.Max(180f * scale, ImGui.GetContentRegionAvail().X - buttonWidth - (ButtonGap * scale));
        var field = this.newPresetName;
        TextField.Draw("##rox-new-preset-name", hint, ref field, fieldWidth, 48);
        this.newPresetName = field;

        ImGui.SameLine(0f, ButtonGap * scale);
        // One identity for both wordings, so the button does not become a different widget the
        // moment the typed name matches a saved preset.
        if (PillButton.Draw("##rox-preset-save", save, Styling.AccentGold, PillButton.Emphasis.Filled, FontAwesomeIcon.Save,
                enabled: name.Length > 0, height: Layout.FieldHeight,
                tooltip: Loc.Get(
                    "presets.save.tooltip",
                    "Saves every setting on Announcements, Appearance, Motion and Fonts. Sound is not "
                    + "included.")))
        {
            if (existing is not null)
            {
                this.config.UserPresets.Remove(existing);
            }

            this.config.UserPresets.Add(UserPreset.Capture(name, this.config));
            this.config.Save();
            this.newPresetName = string.Empty;
        }
    }

    private void DrawCustomFontPresetWarning()
    {
        if (!this.config.UsesCustomFont)
        {
            return;
        }

        Warn(Loc.Get(
            "presets.customfont.warning",
            "These settings use a font from this PC. A preset stores where that file sits, not the "
            + "font itself, so on anyone else's machine the path will not exist and those lines fall "
            + "back to Noto Sans CJK. Everything else in the preset still applies. Put the line "
            + "back on a built-in face if you want a preset to look the same for whoever you hand it to."));
    }

    // A preset carries the path to a custom font, not the font, so a code that travels between
    // machines usually points at a file the recipient does not have.
    private string? MissingCustomFontNote()
    {
        foreach (var role in Enum.GetValues<FontRole>())
        {
            var font = this.config.FontFor(role);

            if (font.IsCustom && FontLimits.CustomFontProblem(font.Path) != null)
            {
                return Loc.Get(
                    "presets.customfont.missing",
                    "It carries a font file from the sender's PC that is not on yours, so those "
                    + "lines are drawn with Noto Sans CJK. Choose your own on the Fonts tab.");
            }
        }

        return null;
    }
}
