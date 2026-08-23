using System.Linq;
using System.Reflection;

namespace RegionsOfXIV;

// Presets and share codes copy settings by reflection rather than by a hand-written list, so a
// new setting travels everywhere the moment it is added to Configuration and nobody has to
// remember to update three places.
//
// NotCopied is what must not travel: identity and bookkeeping, plus settings kept only so an old
// config can be migrated.
internal static class ConfigurationCopy
{
    private static readonly string[] NotCopied =
        ["Version", "UserPresets", "LastSeenVersion", "OverlapHeader"];

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

        to.RepairFaintColors();
    }

    public static void ResetToDefaults(Configuration config) => Apply(new Configuration(), config);
}
