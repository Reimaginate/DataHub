using System.CommandLine;
using Reimaginate.CLI.Base.Abstractions;

namespace Reimaginate.DataHub.CLI.Tools.PluginBase;

public abstract class TopLevelCommand(string commandName, IServiceProvider serviceProvider) : Reimaginate.CLI.Base.Abstractions.TopLevelCommand(commandName, serviceProvider)
{
    public IServiceProvider ServiceProvider { get; } = serviceProvider;

    public Delegate? Handler
    {
        get => null;
        set
        {
            if (value == null)
            {
                return;
            }

            SetAction((parseResult, cancellationToken) =>
                CommandHandlerAdapter.InvokeAsync(this, value, parseResult, cancellationToken));
        }
    }
}

public interface IDataHubTopLevelCommand
{
}

public abstract class DataHubTopLevelCommand(string commandName, IServiceProvider serviceProvider) : TopLevelCommand(commandName, serviceProvider), IDataHubTopLevelCommand;

public abstract class SubCommand<T> : Reimaginate.CLI.Base.Abstractions.SubCommand<T>
    where T : CliCommand
{
    protected SubCommand(string commandName, IServiceProvider serviceProvider)
        : base(commandName, serviceProvider)
    {
        ServiceProvider = serviceProvider;
    }

    protected SubCommand(string commandName, string description, IServiceProvider serviceProvider)
        : base(commandName, description, serviceProvider)
    {
        Description = description;
        ServiceProvider = serviceProvider;
    }

    public IServiceProvider ServiceProvider { get; }

    public Delegate? Handler
    {
        get => null;
        set
        {
            if (value == null)
            {
                return;
            }

            SetAction((parseResult, cancellationToken) =>
                CommandHandlerAdapter.InvokeAsync(this, value, parseResult, cancellationToken));
        }
    }
}
