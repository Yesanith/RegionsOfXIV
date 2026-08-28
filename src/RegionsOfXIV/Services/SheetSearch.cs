using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Lumina.Excel.Sheets;

namespace RegionsOfXIV.Services;

// Debug-only tooling, removed from compilation in every configuration but Debug by the ItemGroup
// in RegionsOfXIV.csproj, so none of this reaches a release build.
internal static class SheetSearch
{
    private const int MaxHits = 40;

    private const int MaxRows = 60_000;

    public static void Run(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            Log.Information("[sheet-find] Usage: /regions find <text>");
            return;
        }

        var getSheet = typeof(Dalamud.Plugin.Services.IDataManager)
            .GetMethods()
            .FirstOrDefault(m => m.Name == "GetExcelSheet" && m.IsGenericMethodDefinition
                                 && m.GetGenericArguments().Length == 1);

        if (getSheet == null)
        {
            Log.Error("[sheet-find] Could not find GetExcelSheet.");
            return;
        }

        var sheets = typeof(Addon).Assembly
            .GetTypes()
            .Where(t => t.IsValueType && t.Namespace == "Lumina.Excel.Sheets")
            .Where(t => TextColumns(t).Length > 0)
            .OrderBy(t => t.Name)
            .ToArray();

        Log.Information($"[sheet-find] \"{query}\" across {sheets.Length} sheets with text columns.");

        var hits = 0;

        foreach (var sheet in sheets)
        {
            hits += Search(getSheet, sheet, query, MaxHits - hits);

            if (hits >= MaxHits)
            {
                Log.Information($"[sheet-find] Stopped at {MaxHits} matches.");
                return;
            }
        }

        Log.Information(hits == 0
            ? "[sheet-find] Nothing anywhere. The wording is not in the sheets."
            : $"[sheet-find] Done, {hits} match(es).");
    }

    public static void Banners()
    {
        var known = BannerNameResolver.All;

        Log.Information($"[banners] {known.Count} named banners:");

        foreach (var (icon, name) in known)
            Log.Information($"[banners] icon={icon,-8} \"{name}\"");

        var unnamed = 0;

        foreach (var image in Plugin.DataManager.GetExcelSheet<ScreenImage>())
        {
            if (image.Image == 0 || known.ContainsKey(image.Image))
                continue;

            Log.Information($"[banners] icon={image.Image,-8} (no stamp names this one)");
            unnamed++;
        }

        Log.Information($"[banners] {unnamed} screen image(s) have no matching stamp.");
    }

    private static PropertyInfo[] TextColumns(Type sheet) =>
        sheet.GetProperties()
            .Where(p => p.PropertyType.Name == "ReadOnlySeString")
            .ToArray();

    private static int Search(MethodInfo getSheet, Type sheet, string query, int allowance)
    {
        object? rows;
        try
        {
            var typed = getSheet.MakeGenericMethod(sheet);
            rows = typed.Invoke(
                Plugin.DataManager,
                Enumerable.Repeat<object?>(null, typed.GetParameters().Length).ToArray());
        }
        catch
        {
            return 0;
        }

        if (rows is not IEnumerable enumerable)
            return 0;

        if (rows.GetType().GetProperty("Count")?.GetValue(rows) is int count && count > MaxRows)
            return 0;

        var columns = TextColumns(sheet);
        var rowId = sheet.GetProperty("RowId");

        var hits = 0;

        foreach (var row in enumerable)
        {
            foreach (var column in columns)
            {
                if (column.GetValue(row)?.ToString() is not { Length: > 0 } text)
                    continue;

                if (!text.Contains(query, StringComparison.OrdinalIgnoreCase))
                    continue;

                Log.Information(
                    $"[sheet-find] {sheet.Name}#{rowId?.GetValue(row)}.{column.Name} = \"{text}\"");

                if (++hits >= allowance)
                    return hits;
            }
        }

        return hits;
    }
}
