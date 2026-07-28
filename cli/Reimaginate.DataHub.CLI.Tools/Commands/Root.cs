using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Reimaginate.DataHub.CLI.Tools.PluginBase;

namespace Reimaginate.DataHub.CLI.Tools.Commands;

public class Root : RootCommand
{
    public Root(IServiceProvider serviceProvider)
    {
        var rootType = typeof(Root);
        var topLevelCommandType = typeof(IDataHubTopLevelCommand);

        var topLevelCommandTypes = rootType
            .Assembly
            .GetExportedTypes()
            .Where(x => topLevelCommandType.IsAssignableFrom(x) && !x.IsAbstract).ToList();

        foreach (var t in topLevelCommandTypes)
        {
            var command = (Command)serviceProvider.GetRequiredService(t);
            Add(command);
        }
    }
}
