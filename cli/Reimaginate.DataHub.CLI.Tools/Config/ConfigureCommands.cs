using Microsoft.Extensions.DependencyInjection;
using Reimaginate.CLI.Base.Abstractions;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using System.CommandLine;
using Command = System.CommandLine.Command;

namespace Reimaginate.DataHub.CLI.Tools.Config;

public static class ConfigureCommands
{
    public static void AddDataHubTools(this RootCommand rootCommand, IServiceProvider serviceProvider)
    {
        var marker = typeof(ConfigureCommands);
        var topLevelCommandType = typeof(IDataHubTopLevelCommand);

        var topLevelCommandTypes = marker
            .Assembly
            .GetExportedTypes()
            .Where(type => topLevelCommandType.IsAssignableFrom(type) && !type.IsAbstract)
            .ToList();

        foreach (var type in topLevelCommandTypes)
        {
            var command = (Command)serviceProvider.GetRequiredService(type);
            var existingCommand = rootCommand.Subcommands.FirstOrDefault(existing => existing.Name == command.Name);
            if (existingCommand is not null)
            {
                rootCommand.Subcommands.Remove(existingCommand);
                rootCommand.Add(command);
                continue;
            }

            rootCommand.Add(command);
        }
    }
}
