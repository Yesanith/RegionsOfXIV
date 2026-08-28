using System.Text;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace RegionsOfXIV.Services;

// Debug-only tooling. Gitignored, and removed from compilation in every configuration but
// Debug by the ItemGroup in RegionsOfXIV.csproj, so none of this reaches a release build.
internal static class AddonNodeDump
{
    public static unsafe void Write(string addonName, AtkUnitBase* addon)
    {
        if (addon == null)
            return;

        var count = addon->UldManager.NodeListCount;

        var sb = new StringBuilder();
        sb.AppendLine($"--- {addonName}: {count} nodes, addon visible={addon->IsVisible} ---");

        for (var i = 0; i < count; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node == null)
                continue;

            sb.Append($"[{i,2}] id={node->NodeId,-4} {node->Type,-10} ");
            sb.Append($"xy=({node->X,7:F1},{node->Y,7:F1}) wh=({node->Width,4}x{node->Height,4}) ");
            sb.Append($"vis={((node->NodeFlags & NodeFlags.Visible) != 0 ? "Y" : "n")} ");
            sb.Append($"tint={Hex(node->Color)}");

            switch (node->Type)
            {
                case NodeType.Text:
                    sb.Append(DescribeText((AtkTextNode*)node));
                    break;

                case NodeType.Image:
                    sb.Append(DescribeImage((AtkImageNode*)node));
                    break;
            }

            sb.AppendLine();
        }

        Log.Information(sb.ToString());
    }

    private static unsafe string DescribeText(AtkTextNode* node) =>
        $" | text font={node->FontSize} align={node->AlignmentFontType:X2} " +
        $"fill={Hex(node->TextColor)} edge={Hex(node->EdgeColor)} " +
        $"flags={node->TextFlags} \"{node->NodeText.ToString()}\"";

    private static unsafe string DescribeImage(AtkImageNode* node)
    {
        var parts = node->PartsList;
        if (parts == null || node->PartId >= parts->PartCount)
            return " | image (no parts list)";

        var part = parts->Parts[node->PartId];
        var rect = $" | image part {node->PartId}/{parts->PartCount} " +
                   $"uv=({part.U},{part.V}) src={part.Width}x{part.Height}";

        var asset = part.UldAsset;
        if (asset == null)
            return $"{rect} (no asset)";

        var texture = asset->AtkTexture;
        if (texture.TextureType != TextureType.Resource || texture.Resource == null)
            return $"{rect} type={texture.TextureType}";

        var handle = texture.Resource->TexFileResourceHandle;
        if (handle == null)
            return $"{rect} icon={texture.Resource->IconId} (not yet loaded)";

        return $"{rect} tex=\"{handle->ResourceHandle.FileName.ToString()}\"";
    }

    private static string Hex(ByteColor color) =>
        $"{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}";
}
