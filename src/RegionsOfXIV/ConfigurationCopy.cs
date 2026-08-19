using System.Linq;
using System.Reflection;

namespace RegionsOfXIV;

internal static class ConfigurationCopy
{
    private static readonly string[] NotCopied = ["Version", "UserPresets", "LastSeenVersion"];

    public static readonly PropertyInfo[] Settings =
        typeof(Configuration)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && !NotCopied.Contains(p.Name))
            .ToArray();

    public static PropertyInfo? Find(string name) =>
        Settings.FirstOrDefault(p => p.Name == name);

    public static void Apply(Configuration from, Configuration to)
    {
        foreach (var property in Settings)
            property.SetValue(to, property.GetValue(from));
    }

    public static void ResetToDefaults(Configuration config) => Apply(new Configuration(), config);
}
