using System;
using System.Collections.Generic;
using Dalamud.Game.Command;
using RegionsOfXIV.Services;

namespace RegionsOfXIV;

// The whole of the /regions surface: registering it, the help line, and deciding what each
// subcommand does.
//
// The subcommands arrive as a dictionary rather than as a parameter each, because which ones
// exist depends on the build. Plugin.cs adds the debug entries inside its own #if DEBUG, next to
// the windows they toggle, so there is one place that knows a debug build has more of them.
internal sealed class CommandRouter : IDisposable
{
    public const string Name = "/regions";

    private readonly Action openSettings;
    private readonly IReadOnlyDictionary<string, Action> subcommands;

    public CommandRouter(Action openSettings, IReadOnlyDictionary<string, Action> subcommands)
    {
        this.openSettings = openSettings;
        this.subcommands = subcommands;

        Plugin.CommandManager.AddHandler(Name, new CommandInfo(Handle)
        {
            // Shown in Dalamud's own command list, so it names only the subcommands a release
            // build has -- the debug ones are not there to be found.
            //
            // The subcommands are passed in rather than written into the sentence: they are what
            // the player types, so a translated one would name a command that does not exist.
            //
            // Built once, here, which is after Plugin has applied the language -- so it is in the
            // right language at load. Dalamud keeps the string it was given rather than asking
            // again, so a language change afterwards leaves this one entry in the old language
            // until the plugin is reloaded.
            HelpMessage = Loc.Format(
                "command.help",
                "Open the Regions of XIV settings. \"{0}\" fires a sample notification, "
                + "\"{1}\" shows what has changed.",
                Name + " test",
                Name + " changelog"),
        });
    }

    public void Dispose() => Plugin.CommandManager.RemoveHandler(Name);

    private void Handle(string command, string args)
    {
        var argument = args.Trim();

        // Bare /regions opens the settings, matching the installer's own button for the plugin.
        if (argument.Length == 0)
        {
            this.openSettings();
            return;
        }

        if (this.subcommands.TryGetValue(argument, out var action))
        {
            action();
            return;
        }

#if DEBUG
        // The one subcommand that carries an argument, so it cannot be a dictionary entry.
        if (argument.StartsWith("find ", StringComparison.OrdinalIgnoreCase))
        {
            SheetSearch.Run(argument[5..].Trim());
            return;
        }
#endif

        this.openSettings();
    }
}
