using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;
using RegionsOfXIV.Services;

namespace RegionsOfXIV.UI;

// Debug-only tooling, removed from compilation in every configuration but Debug by the ItemGroup
// in RegionsOfXIV.csproj, so none of this reaches a release build.
internal sealed class IconBrowserWindow : Window, IDisposable
{
    private const float RowHeight = 56f;

    private const float LabelWidth = 150f;

    private const float CloseUpWidth = 620f;

    private string query = string.Empty;
    private bool onlyUnnamed = true;

    private uint[] shown = [];
    private string shownFor = "\0";
    private bool shownUnnamed;

    public IconBrowserWindow()
        : base("Icon browser###RegionsOfXIVIcons")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 300),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };

        Size = new Vector2(560, 620);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.TextWrapped(
            "Every image the game can put on screen. Blank means the client has no such "
            + "icon. Type IDs or ranges to narrow it down, for example \"120021\" or "
            + "\"120001-120130\". Hover an image to see it full size.");

        ImGui.Spacing();

        var text = this.query;
        if (ImGui.InputTextWithHint("##icon-query", "all screen images", ref text, 128))
            this.query = text;

        ImGui.SameLine();

        var unnamed = this.onlyUnnamed;
        if (ImGui.Checkbox("Only ones we cannot name", ref unnamed))
            this.onlyUnnamed = unnamed;

        var ids = Shown();

        ImGui.Separator();
        ImGui.TextDisabled($"{ids.Length} icon(s)");

        using var list = ImRaii.Child("##icon-list", Vector2.Zero, true);
        if (!list)
            return;

        var height = RowHeight * ImGuiHelpers.GlobalScale;

        foreach (var id in ids)
            DrawRow(id, height);
    }

    private void DrawRow(uint id, float height)
    {
        if (!ImGui.IsRectVisible(new Vector2(ImGui.GetContentRegionAvail().X, height)))
        {
            ImGui.Dummy(new Vector2(0f, height));
            return;
        }

        var top = ImGui.GetCursorPos();

        ImGui.BeginGroup();
        ImGui.TextUnformatted(id.ToString(CultureInfo.InvariantCulture));
        ImGui.TextDisabled(BannerNameResolver.Resolve(id) ?? "unnamed");
        ImGui.EndGroup();

        ImGui.SetCursorPos(new Vector2(top.X + (LabelWidth * ImGuiHelpers.GlobalScale), top.Y));
        DrawArt(id, height);

        ImGui.SetCursorPos(top);
        ImGui.Dummy(new Vector2(0f, height));

        ImGui.Separator();
    }

    private static void DrawArt(uint id, float height)
    {
        if (GameIcon.Get(id) is not { } wrap || wrap.Size.Y <= 0f)
        {
            ImGui.Dummy(new Vector2(height, height));
            return;
        }

        var ratio = wrap.Size.X / wrap.Size.Y;
        var room = ImGui.GetContentRegionAvail().X;

        ImGui.Image(wrap.Handle, new Vector2(Math.Min(height * ratio, room), height));

        if (!ImGui.IsItemHovered())
            return;

        var width = Math.Min(wrap.Size.X, CloseUpWidth * ImGuiHelpers.GlobalScale);

        using var tooltip = ImRaii.Tooltip();

        ImGui.Image(wrap.Handle, new Vector2(width, width / ratio));
    }

    private uint[] Shown()
    {
        if (this.shownFor == this.query && this.shownUnnamed == this.onlyUnnamed)
            return this.shown;

        this.shownFor = this.query;
        this.shownUnnamed = this.onlyUnnamed;

        var ids = Parse(this.query) ?? ScreenImages();

        if (this.onlyUnnamed)
            ids = ids.Where(id => BannerNameResolver.Resolve(id) == null);

        this.shown = ids.Distinct().OrderBy(id => id).ToArray();

        return this.shown;
    }

    private static IEnumerable<uint> ScreenImages()
    {
        foreach (var image in Plugin.DataManager.GetExcelSheet<ScreenImage>())
        {
            if (image.Image != 0)
                yield return image.Image;
        }
    }

    private static IEnumerable<uint>? Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var ids = new List<uint>();

        foreach (var part in text.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries))
        {
            var range = part.Split('-', StringSplitOptions.RemoveEmptyEntries);

            if (range.Length == 2 &&
                uint.TryParse(range[0], out var from) &&
                uint.TryParse(range[1], out var to) &&
                to >= from)
            {
                for (var id = from; id <= Math.Min(to, from + 2000); id++)
                    ids.Add(id);

                continue;
            }

            if (uint.TryParse(part, out var single))
                ids.Add(single);
        }

        return ids.Count == 0 ? null : ids;
    }
}
