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

    // Settings that no longer travel, but that an older preset can still name. Migrate() works
    // from the value the old build wrote, so importing has to be able to set one even though
    // saving no longer writes it -- otherwise the migration has nothing to read and whatever
    // replaced it silently lands on its default.
    private static readonly string[] ReadOnlyForMigration = ["OverlapHeader"];

    public static readonly PropertyInfo[] Settings =
        typeof(Configuration)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && !NotCopied.Contains(p.Name))
            .ToArray();

    private static readonly PropertyInfo[] Superseded =
        typeof(Configuration)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && ReadOnlyForMigration.Contains(p.Name))
            .ToArray();

    public static PropertyInfo? Find(string name) =>
        Settings.FirstOrDefault(p => p.Name == name);

    // Used when reading a preset in rather than writing one out: finds the superseded settings
    // too, so they are available to the migration that runs straight afterwards.
    public static PropertyInfo? FindForImport(string name) =>
        Find(name) ?? Superseded.FirstOrDefault(p => p.Name == name);

    public static void Apply(Configuration from, Configuration to)
    {
        foreach (var property in Settings)
            property.SetValue(to, property.GetValue(from));

        to.RepairFaintColors();
    }

    public static void ResetToDefaults(Configuration config) => Apply(new Configuration(), config);
}
