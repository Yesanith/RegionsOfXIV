using System.Linq;
using System.Reflection;

namespace RegionsOfXIV;

// Writing one Configuration's settings over another's.
//
// By reflection rather than property by property, and that is the point: a
// setting added to Configuration later belongs to presets immediately, with no
// second place to remember. Hand-written assignments would silently omit it, and
// the omission would surface much later as "this preset does not restore X".
//
// Both kinds of preset go through here. A saved one copies from the snapshot it
// stored; a built-in one copies from a fresh Configuration — i.e. resets to the
// defaults — and then layers its own look on top.
internal static class ConfigurationCopy
{
    // None of these three is a preference.
    //
    // Version says which shape the file is in. A preset holding the preset list
    // would nest without end. LastSeenVersion records which changelog this player
    // has read — carrying it would let an imported preset either re-show the
    // changelog or suppress the next one, depending on whose machine the code came
    // from.
    private static readonly string[] NotCopied = ["Version", "UserPresets", "LastSeenVersion"];

    // Every property that counts as a setting, resolved once. The set cannot change
    // at runtime, and nothing here runs in the draw path, but there is no reason to
    // walk the type every time either.
    //
    // Public because a share code has to agree with a copy about what a setting is.
    // One list, so a property added to Configuration is picked up by both at once.
    public static readonly PropertyInfo[] Settings =
        typeof(Configuration)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && !NotCopied.Contains(p.Name))
            .ToArray();

    // Null for a name this build does not have — which is what a code written by a
    // newer version looks like from here.
    public static PropertyInfo? Find(string name) =>
        Settings.FirstOrDefault(p => p.Name == name);

    public static void Apply(Configuration from, Configuration to)
    {
        foreach (var property in Settings)
            property.SetValue(to, property.GetValue(from));
    }

    // Every setting back to the value its initializer gives it in Configuration,
    // which stays the single place the defaults are written down.
    public static void ResetToDefaults(Configuration config) => Apply(new Configuration(), config);
}
