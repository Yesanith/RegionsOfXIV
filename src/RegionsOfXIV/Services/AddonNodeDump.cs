using System.Text;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace RegionsOfXIV.Services;

// Diagnostic only, and CURRENTLY UNWIRED — nothing calls Write. Kept while it is
// still unclear whether TerritoryInfo names settlements such as Aleport; delete
// this file once that is settled.
//
// To re-enable: call Write(args.AddonName, addon) from NativeUiSuppressor's
// content events (not PreDraw, which fires every frame).
//
// Walks an addon's node list and writes what each node is to the plugin log. The
// loading-screen addons exist for a couple of seconds and cannot be inspected by
// hand, so running this from their own setup callback and reading the log
// afterwards is the only practical way to see inside them.
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

        Plugin.Log.Information(sb.ToString());
    }

    private static unsafe string DescribeText(AtkTextNode* node) =>
        $" | text font={node->FontSize} align={node->AlignmentFontType:X2} " +
        $"fill={Hex(node->TextColor)} edge={Hex(node->EdgeColor)} " +
        $"flags={node->TextFlags} \"{node->NodeText.ToString()}\"";

    // The texture path is the useful part: it is what ITextureProvider.GetFromGame
    // takes, so anything the addon draws can be redrawn by us. The part rectangle
    // is the source region within that atlas.
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
