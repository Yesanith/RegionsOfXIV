using System;
using System.IO;

namespace RegionsOfXIV;

// Reading the stored configuration, and everything that has to happen to it before the rest of
// the plugin may see it: migration, the repair pass, and setting a file aside when it cannot be
// read at all.
//
// Apart from Plugin.cs itself this is the only thing that touches the file on disk, which is what
// lets the composition root stay a list of constructor calls.
internal static class ConfigurationStore
{
    public static (Configuration Config, bool IsFirstRun) Load()
    {
        try
        {
            if (Plugin.PluginInterface.GetPluginConfig() is Configuration stored)
            {
                var changed = stored.Migrate();
                changed |= MigrateSavedPresets(stored);
                changed |= stored.RepairFaintColors();

                if (changed)
                    stored.Save();

                return (stored, false);
            }

            return (new Configuration(), true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not read the stored configuration; falling back to defaults.");
            Quarantine();
            return (new Configuration(), false);
        }
    }

    // A saved preset is a whole configuration of its own, written by whichever build saved it, so
    // it is exactly as old as the file it sits in and needs the same migration. Without this,
    // applying a preset saved before a setting was replaced quietly resets that setting to its
    // default -- the preset still holds the old value, but nothing reads it any more.
    private static bool MigrateSavedPresets(Configuration config)
    {
        var changed = false;

        foreach (var preset in config.UserPresets)
        {
            if (preset.Settings is { } settings)
                changed |= settings.Migrate();
        }

        return changed;
    }

    // A config that cannot be parsed is moved aside rather than deleted or overwritten, so the
    // plugin starts on defaults and whatever the user had is still recoverable by hand.
    private static void Quarantine()
    {
        try
        {
            var file = Plugin.PluginInterface.ConfigFile;
            if (!file.Exists)
                return;

            var target = Path.Combine(
                file.DirectoryName!,
                $"{Path.GetFileNameWithoutExtension(file.Name)}.broken-{DateTime.Now:yyyyMMdd-HHmmss}.json");

            file.MoveTo(target, overwrite: true);
            Log.Information($"Moved the unreadable configuration to {target}");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not set aside the unreadable configuration.");
        }
    }
}
