using System.Linq;
using System.Reflection;

namespace RegionsOfXIV;

// Presets and share codes copy settings by reflection rather than by a hand-written list, so a
// new setting travels everywhere the moment it is added to Configuration and nobody has to
// remember to update three places.
//
// NotCopied is what must not travel: identity and bookkeeping, settings kept only so an old
// config can be migrated, and the interface language -- a preset is a look, and a code arriving
// from another player has no business changing which language someone reads the window in.
//
// The sound settings are in there on a stronger version of the same argument. A colour arriving
// from a stranger is visible the moment it lands and is undone by looking at it; a sound is not.
// It happens while the player is doing something else, it is startling rather than merely wrong,
// and nothing on screen says which setting caused it or that a share code turned it on. Switching
// sound on is a consent decision rather than an aesthetic one.
//
// There is also nothing to share. The sounds are the game's own sixteen, so a preset could carry
// "makes a noise" and a number, not a mood.
//
// SoundFilePath is in there for a second reason on top of that one. It is a path on the sender's
// own machine, so it is worth nothing to a recipient and it usually contains their Windows account
// name. The custom font paths do travel, deliberately, and the Presets tab warns about them; a
// sound file is not worth that trade, because unlike a font there is nothing at the far end that
// could ever resolve it into the thing the sender heard.
internal static class ConfigurationCopy
{
    private static readonly string[] NotCopied =
    [
        "Version", "UserPresets", "LastSeenVersion", "OverlapHeader",
        "Language", "TranslationNoticeDismissedFor", "BannerNameLanguage",
        "SoundSource", "GameSoundId", "SoundFilePath",
        "SoundOnLocation", "SoundOnWeather", "SoundOnBanner",
    ];

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
