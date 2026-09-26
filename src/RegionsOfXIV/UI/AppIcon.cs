using System.IO;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace RegionsOfXIV.UI;

// The plugin's own icon, read from the images folder the build copies it to. Falls back to a
// gold tile with a glyph if the file is not there, so a missing asset costs a picture and nothing else.
internal static class AppIcon
{
    private const string FileName = "icon.png";

    private static string? Path;
    private static bool? Exists;

    public static void Draw(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, float alpha = 1f)
    {
        Path ??= System.IO.Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty, "images", FileName);
        Exists ??= File.Exists(Path);

        if (Exists.Value)
        {
            var texture = Plugin.TextureProvider.GetFromFile(Path).GetWrapOrEmpty();
            drawList.AddImageRounded(texture.Handle, min, max, Vector2.Zero, Vector2.One,
                Paint.Col(new Vector4(1f, 1f, 1f, alpha)), rounding, ImDrawFlags.RoundCornersAll);
            return;
        }

        Paint.Gradient(drawList, min, max, Styling.AccentGoldSoft, Styling.AccentGold, rounding);
        TextDraw.IconCentered(FontAwesomeIcon.MapMarkedAlt, (min + max) * 0.5f, Styling.WithAlpha(Styling.InkOnGold, alpha));
    }
}
