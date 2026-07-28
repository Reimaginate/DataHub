using System.CommandLine;
using Reimaginate.DataHub.CLI.Tools.Commands.Delete.Entities;
using Reimaginate.DataHub.CLI.Tools.Commands.Delete.Jobs;
using Reimaginate.DataHub.CLI.Tools.Commands.Delete.Role;
using Reimaginate.DataHub.CLI.Tools.Commands.Disable.User;
using Reimaginate.DataHub.CLI.Tools.Commands.Enable.User;
using Reimaginate.DataHub.CLI.Tools.Commands.Export.Entities;
using Reimaginate.DataHub.CLI.Tools.Commands.Get.Alerts;
using Reimaginate.DataHub.CLI.Tools.Commands.Get.AlternateKeys;
using Reimaginate.DataHub.CLI.Tools.Commands.Get.Duplicates;
using Reimaginate.DataHub.CLI.Tools.Commands.Get.Entities;
using Reimaginate.DataHub.CLI.Tools.Commands.Get.Jobs;
using Reimaginate.DataHub.CLI.Tools.Commands.Get.Locks;
using Reimaginate.DataHub.CLI.Tools.Commands.Get.Logs;
using Reimaginate.DataHub.CLI.Tools.Commands.Get.Permissions;
using Reimaginate.DataHub.CLI.Tools.Commands.Get.Roles;
using Reimaginate.DataHub.CLI.Tools.Commands.Get.TrackingData;
using Reimaginate.DataHub.CLI.Tools.Commands.Import.Entities;
using Reimaginate.DataHub.CLI.Tools.Commands.Patch.Entities;
using Reimaginate.DataHub.CLI.Tools.Commands.Register.AlternateKeys;
using Reimaginate.DataHub.CLI.Tools.Commands.Register.Role;
using Reimaginate.DataHub.CLI.Tools.Commands.Register.User;
using Reimaginate.DataHub.CLI.Tools.Commands.Submit.Job;
using Reimaginate.DataHub.CLI.Tools.Commands.Update.Role;
using Reimaginate.DataHub.CLI.Tools.Commands.Update.User;
using Reimaginate.DataHub.CLI.Tools.PluginBase;
using Reimaginate.DataHub.CLI.Tools.Shared.Runtime;
using static Reimaginate.DataHub.CLI.Tools.Commands.Active.ActiveCommandFactory;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Active;

public sealed class EntitiesCommand : DataHubTopLevelCommand
{
    public EntitiesCommand(IServiceProvider serviceProvider) : base("entities", serviceProvider)
    {
        Description = "Manage DataHub entities.";
        Add(EntityList(serviceProvider));
        Add(EntityGet(serviceProvider));
        Add(EntityDelete(serviceProvider));
        Add(EntityImport(serviceProvider));
        Add(EntityPatch(serviceProvider));
        Add(new EntitiesUpdateCommand(serviceProvider));
        Add(new EntitiesRevertCommand(serviceProvider));
        Add(new EntitiesSplitAltKeyCommand(serviceProvider));
        Add(EntityDetach(serviceProvider));
        Add(EntityRebase(serviceProvider));
        Add(EntityFindByAltKey(serviceProvider));
        Add(EntityCounts(serviceProvider));
    }

    private static Command EntityList(IServiceProvider services)
    {
        var command = NewCommand("list", "List entities by query.", out var output);
        var where = OptionalArgument("where", "DataHub query filter.");
        var properties = StringOption("--properties", "Properties to display.", "--props", "-p");
        var additionalProperties = StringOption("--additional-properties", "Additional properties to include.", "--add-props");
        var pageSize = IntOption("--page-size", "Page size for query results.", "--page");
        var info = BoolOption("--info", "Show entity count information.", "-i");
        var saveTo = StringOption("--save-to", "Save results to a file.", "-s");
        var dontOpen = BoolOption("--dont-open", "Do not open saved output.", "--no");
        var expand = BoolOption("--expand-results", "Expand nested result values.", "--expand");
        command.Add(where);
        ActiveCommandFactory.Add(command, properties, additionalProperties, pageSize, info, saveTo, dontOpen, expand);
        command.SetAction((parse, ct) => RunLegacyAsync(parse, output, () =>
        {
            var query = parse.GetValue(where);
            if (string.IsNullOrWhiteSpace(query))
            {
                Console.Error.WriteLine("Usage: datahub entities list <where> [options]");
                return Task.FromResult(CliExitCodes.Usage);
            }

            return new GetEntitiesWhereCommand(services).HandleCommand(
                query,
                parse.GetValue(properties),
                parse.GetValue(additionalProperties),
                parse.GetValue(pageSize),
                parse.GetValue(info),
                parse.GetValue(saveTo) ?? string.Empty,
                parse.GetValue(dontOpen),
                parse.GetValue(expand),
                ct);
        }));
        return command;
    }

    private static Command EntityGet(IServiceProvider services)
    {
        var command = NewCommand("get", "Get entities by id.", out var output);
        var entityType = RequiredArgument("entityType", "DataHub entity type.");
        var ids = RequiredManyArgument("ids", "Entity ids.");
        var properties = StringOption("--properties", "Properties to display.", "--props", "-p");
        var additionalProperties = StringOption("--additional-properties", "Additional properties to include.", "--add-props");
        var pageSize = IntOption("--page-size", "Page size for output.", "--page");
        var info = BoolOption("--info", "Show entity information.", "-i");
        var saveTo = StringOption("--save-to", "Save results to a file.", "-s");
        var dontOpen = BoolOption("--dont-open", "Do not open saved output.", "--no");
        var expand = BoolOption("--expand-results", "Expand nested result values.", "--expand");
        ActiveCommandFactory.Add(command, entityType, ids, properties, additionalProperties, pageSize, info, saveTo, dontOpen, expand);
        command.SetAction((parse, ct) => RunLegacyAsync(parse, output, () =>
            new GetEntitiesByIdCommand(services).HandleCommand(
                parse.GetValue(entityType)!,
                parse.GetValue(ids) ?? [],
                parse.GetValue(properties),
                parse.GetValue(additionalProperties),
                parse.GetValue(pageSize),
                parse.GetValue(info),
                parse.GetValue(saveTo) ?? string.Empty,
                parse.GetValue(dontOpen),
                parse.GetValue(expand),
                ct)));
        return command;
    }

    private static Command EntityDelete(IServiceProvider services)
    {
        var command = NewCommand("delete", "Delete entities by id.", out var output);
        var entityType = RequiredArgument("entityType", "DataHub entity type.");
        var ids = OptionalManyArgument("ids", "Entity ids.");
        var fromFile = StringOption("--from-file", "Read entity ids from a file.", "-f");
        var dryRun = BoolOption("--dry-run", "Preview entity ids without deleting them.");
        var yes = BoolOption("--yes", "Confirm deletion without prompting.");
        ActiveCommandFactory.Add(command, entityType, ids, fromFile, dryRun, yes);
        command.SetAction((parse, ct) => RunLegacyAsync(parse, output, () =>
            new DeleteEntitiesByIdCommand(services).HandleCommandWithOptions(parse.GetValue(entityType)!, parse.GetValue(ids) ?? [], parse.GetValue(fromFile), parse.GetValue(dryRun), parse.GetValue(yes), ct)));
        return command;
    }

    private static Command EntityImport(IServiceProvider services)
    {
        var command = FileOperationCommand("import", "Import entities.", out var output);
        var dataOnly = BoolOption("--data-only", "Import files contain raw record data.");
        var overwrite = BoolOption("--overwrite", "Overwrite/update existing records if found.");
        var untracked = BoolOption("--untracked", "Do not add tracking entries.");
        var notifyAgents = BoolOption("--notify-agents", "Dispatch notifications to agents.");
        var dryRun = BoolOption("--dry-run", "Preview import input without writing entity data.");
        var yes = BoolOption("--yes", "Confirm workload warnings without prompting.");
        command.Add(dataOnly);
        command.Add(overwrite);
        command.Add(untracked);
        command.Add(notifyAgents);
        command.Add(dryRun);
        command.Add(yes);
        command.SetAction((parse, ct) => RunLegacyAsync(parse, output, () =>
            new ImportEntitiesCommand(services).HandleCommand(
                parse.GetValue<string>("--source")!,
                parse.GetValue<string?>("--path"),
                parse.GetValue<string?>("--conn"),
                parse.GetValue<string?>("--container"),
                parse.GetValue<string?>("--pattern"),
                parse.GetValue(dataOnly),
                parse.GetValue<bool>("--silent"),
                parse.GetValue(overwrite),
                parse.GetValue(untracked),
                parse.GetValue(notifyAgents),
                parse.GetValue<bool>("--continue"),
                parse.GetValue(dryRun),
                parse.GetValue(yes),
                ct)));
        return command;
    }

    private static Command EntityPatch(IServiceProvider services)
    {
        var command = FileOperationCommand("patch", "Patch entities.", out var output);
        var where = StringOption("--where", "DataHub query selecting entities to patch.");
        var notifyAgents = BoolOption("--notify-agents", "Dispatch notifications to agents.");
        var dryRun = BoolOption("--dry-run", "Preview patch input and matched entities without applying patches.");
        var yes = BoolOption("--yes", "Confirm patch warnings without prompting.");
        command.Add(where);
        command.Add(notifyAgents);
        command.Add(dryRun);
        command.Add(yes);
        command.SetAction((parse, ct) => RunLegacyAsync(parse, output, () =>
            new PatchEntitiesCommand(services).HandleCommand(
                parse.GetValue<string>("--source")!,
                parse.GetValue<string?>("--path"),
                parse.GetValue(where),
                parse.GetValue<string?>("--conn"),
                parse.GetValue<string?>("--container"),
                parse.GetValue<string?>("--pattern"),
                parse.GetValue<bool>("--silent"),
                parse.GetValue(notifyAgents),
                parse.GetValue<bool>("--continue"),
                parse.GetValue(dryRun),
                parse.GetValue(yes),
                ct)));
        return command;
    }

    private static Command EntityDetach(IServiceProvider services)
    {
        var command = NewCommand("detach", "Detach entities from a data source.", out var output);
        var where = RequiredArgument("where", "DataHub query filter.");
        var dataSource = RequiredArgument("dataSource", "Data source to detach.");
        ActiveCommandFactory.Add(command, where, dataSource);
        command.SetAction((parse, ct) => RunLegacyAsync(parse, output, () =>
            new GetEntitiesDetachCommand(services).HandleCommand(parse.GetValue(where)!, parse.GetValue(dataSource)!, ct)));
        return command;
    }

    private static Command EntityRebase(IServiceProvider services)
    {
        var command = NewCommand("rebase", "Rebase DataHub entities.", out var output);
        var where = RequiredArgument("where", "DataHub query filter.");
        var rebaseTo = RequiredArgument("rebaseTo", "Target timestamp, or 'now'.");
        ActiveCommandFactory.Add(command, where, rebaseTo);
        command.SetAction((parse, _) => RunLegacyAsync(parse, output, () =>
            new GetEntitiesRebaseCommand(services).HandleCommand(parse.GetValue(where)!, parse.GetValue(rebaseTo)!)));
        return command;
    }

    private static Command EntityFindByAltKey(IServiceProvider services)
    {
        var command = NewCommand("find-by-alt-key", "Find entities by alternate key.", out var output);
        var key = StringOption("--key", "Alternate key name.");
        key.Required = true;
        var value = StringOption("--value", "Alternate key value.");
        value.Required = true;
        var properties = StringOption("--properties", "Properties to display.", "--props", "-p");
        var additional = StringOption("--additional-properties", "Additional properties to include.", "--add-props");
        ActiveCommandFactory.Add(command, key, value, properties, additional);
        command.SetAction((parse, ct) => RunLegacyAsync(parse, output, () =>
            new GetEntitiesByAltKeyCommand(services).HandleCommand(parse.GetValue(key)!, parse.GetValue(value)!, parse.GetValue(properties), parse.GetValue(additional), ct)));
        return command;
    }

    private static Command EntityCounts(IServiceProvider services)
    {
        var command = NewCommand("counts", "Count entities by entity type.", out var output);
        var where = OptionalArgument("where", "Optional DataHub query filter.");
        command.Add(where);
        command.SetAction((parse, ct) => RunLegacyAsync(parse, output, () =>
            new GetEntityCountsCommand(services).HandleCommand(parse.GetValue(where), ct)));
        return command;
    }
}

public sealed class ActiveExportCommand : DataHubTopLevelCommand
{
    public ActiveExportCommand(IServiceProvider serviceProvider) : base("export", serviceProvider)
    {
        Description = "Export entities by id.";
        var output = ActiveCommandFactory.OutputOption(this);
        var entityType = ActiveCommandFactory.RequiredArgument("entityType", "DataHub entity type.");
        var ids = ActiveCommandFactory.RequiredManyArgument("entityIds", "Entity ids.");
        var saveTo = ActiveCommandFactory.StringOption("--save-to", "Output file or folder.", "-s");
        ActiveCommandFactory.Add(this, entityType, ids, saveTo);
        SetAction((parse, ct) => ActiveCommandFactory.RunActiveAsync(parse, output, () =>
            new ExportEntitiesByIdCommand(serviceProvider).HandleCommand(parse.GetValue(entityType)!, parse.GetValue(ids) ?? [], parse.GetValue(saveTo), ct)));
    }
}

public sealed class DataCommand : DataHubTopLevelCommand
{
    public DataCommand(IServiceProvider serviceProvider) : base("data", serviceProvider)
    {
        Description = "Manage DataHub data operations.";
        Add(DataDuplicates(serviceProvider));
    }

    private static Command DataDuplicates(IServiceProvider services)
    {
        var command = new Command("duplicates", "Inspect duplicate records.");
        command.Add(DuplicatesList(services));
        command.Add(DuplicatesGet(services));
        return command;
    }

    private static Command DuplicatesList(IServiceProvider services)
    {
        var command = ActiveCommandFactory.NewCommand("list", "List duplicates.", out var output);
        var where = ActiveCommandFactory.OptionalArgument("where", "Optional duplicate query filter.");
        var properties = ActiveCommandFactory.StringOption("--properties", "Properties to display.", "--props", "-p");
        var additional = ActiveCommandFactory.StringOption("--additional-properties", "Additional properties to include.", "--add-props");
        var pageSize = ActiveCommandFactory.IntOption("--page-size", "Page size.", "--page");
        var info = ActiveCommandFactory.BoolOption("--info", "Show duplicate count information.", "-i");
        var saveTo = ActiveCommandFactory.StringOption("--save-to", "Save results to a file.", "-s");
        var dontOpen = ActiveCommandFactory.BoolOption("--dont-open", "Do not open saved output.", "--no");
        ActiveCommandFactory.Add(command, where, properties, additional, pageSize, info, saveTo, dontOpen);
        command.SetAction((parse, ct) => ActiveCommandFactory.RunLegacyAsync(parse, output, () =>
            new GetDuplicatesWhereCommand(services).HandleCommand(parse.GetValue(where), parse.GetValue(properties), parse.GetValue(additional), parse.GetValue(pageSize), parse.GetValue(info), parse.GetValue(saveTo) ?? string.Empty, parse.GetValue(dontOpen), ct)));
        return command;
    }

    private static Command DuplicatesGet(IServiceProvider services)
    {
        var command = ActiveCommandFactory.NewCommand("get", "Get a duplicate by id.", out var output);
        var id = ActiveCommandFactory.RequiredArgument("id", "Duplicate id.");
        var properties = ActiveCommandFactory.StringOption("--properties", "Properties to display.", "--props", "-p");
        var additional = ActiveCommandFactory.StringOption("--additional-properties", "Additional properties to include.", "--add-props");
        ActiveCommandFactory.Add(command, id, properties, additional);
        command.SetAction((parse, ct) => ActiveCommandFactory.RunLegacyAsync(parse, output, () =>
            new GetDuplicateByIdCommand(services).HandleCommand(parse.GetValue(id)!, parse.GetValue(properties), parse.GetValue(additional), ct)));
        return command;
    }
}

public sealed class SourceEntitiesCommand : DataHubTopLevelCommand
{
    public SourceEntitiesCommand(IServiceProvider serviceProvider) : base("source-entities", serviceProvider)
    {
        Description = "Repair source-entity tracking data.";
        var command = ActiveCommandFactory.NewCommand("rebase", "Rebase source entities matched by a DataHub entity query.", out var output);
        var where = ActiveCommandFactory.RequiredArgument("where", "DataHub query filter.");
        var dataSource = ActiveCommandFactory.RequiredArgument("dataSource", "Source data source.");
        var rebaseTo = ActiveCommandFactory.RequiredArgument("rebaseTo", "Target timestamp, or 'now'.");
        ActiveCommandFactory.Add(command, where, dataSource, rebaseTo);
        command.SetAction((parse, _) => ActiveCommandFactory.RunLegacyAsync(parse, output, () =>
            new GetSourceEntitiesRebaseCommand(serviceProvider).HandleCommand(parse.GetValue(where)!, parse.GetValue(dataSource)!, parse.GetValue(rebaseTo)!)));
        Add(command);
    }
}

public sealed class JobsCommand : DataHubTopLevelCommand
{
    public JobsCommand(IServiceProvider serviceProvider) : base("jobs", serviceProvider)
    {
        Description = "Manage DataHub jobs.";
        Add(JobList(serviceProvider));
        Add(JobQuery(serviceProvider));
        Add(JobGet(serviceProvider));
        Add(JobDelete(serviceProvider));
        Add(JobSubmit(serviceProvider));
        Add(JobRetry(serviceProvider));
    }

    private static Command JobList(IServiceProvider services)
    {
        var command = ActiveCommandFactory.NewCommand("list", "List jobs.", out var output);
        var where = ActiveCommandFactory.OptionalArgument("where", "Optional job query filter.");
        var properties = ActiveCommandFactory.StringOption("--properties", "Properties to display.", "--props", "-p");
        var additional = ActiveCommandFactory.StringOption("--additional-properties", "Additional properties to include.", "--add-props");
        var pageSize = ActiveCommandFactory.IntOption("--page-size", "Page size.", "--page");
        var info = ActiveCommandFactory.BoolOption("--info", "Show job count information.", "-i");
        var saveTo = ActiveCommandFactory.StringOption("--save-to", "Save results to a file.", "-s");
        var dontOpen = ActiveCommandFactory.BoolOption("--dont-open", "Do not open saved output.", "--no");
        ActiveCommandFactory.Add(command, where, properties, additional, pageSize, info, saveTo, dontOpen);
        command.SetAction((parse, ct) => ActiveCommandFactory.RunLegacyAsync(parse, output, () =>
        {
            var query = parse.GetValue(where);
            return string.IsNullOrWhiteSpace(query)
                ? new GetJobsCommand(services).HandleCommand(parse.GetValue(properties), parse.GetValue(additional), parse.GetValue(pageSize), parse.GetValue(info), parse.GetValue(saveTo) ?? string.Empty, parse.GetValue(dontOpen), ct)
                : new GetJobsWhereCommand(services).HandleCommand(query, parse.GetValue(properties), parse.GetValue(additional), parse.GetValue(pageSize), parse.GetValue(info), parse.GetValue(saveTo) ?? string.Empty, parse.GetValue(dontOpen), ct);
        }));
        return command;
    }

    private static Command JobQuery(IServiceProvider services)
    {
        var command = ActiveCommandFactory.NewCommand("query", "Query jobs.", out var output);
        var where = ActiveCommandFactory.RequiredArgument("where", "Job query filter.");
        var properties = ActiveCommandFactory.StringOption("--properties", "Properties to display.", "--props", "-p");
        var additional = ActiveCommandFactory.StringOption("--additional-properties", "Additional properties to include.", "--add-props");
        var pageSize = ActiveCommandFactory.IntOption("--page-size", "Page size.", "--page");
        var info = ActiveCommandFactory.BoolOption("--info", "Show job count information.", "-i");
        var saveTo = ActiveCommandFactory.StringOption("--save-to", "Save results to a file.", "-s");
        var dontOpen = ActiveCommandFactory.BoolOption("--dont-open", "Do not open saved output.", "--no");
        ActiveCommandFactory.Add(command, where, properties, additional, pageSize, info, saveTo, dontOpen);
        command.SetAction((parse, ct) => ActiveCommandFactory.RunLegacyAsync(parse, output, () =>
            new GetJobsWhereCommand(services).HandleCommand(parse.GetValue(where)!, parse.GetValue(properties), parse.GetValue(additional), parse.GetValue(pageSize), parse.GetValue(info), parse.GetValue(saveTo) ?? string.Empty, parse.GetValue(dontOpen), ct)));
        return command;
    }

    private static Command JobGet(IServiceProvider services)
    {
        var command = ActiveCommandFactory.NewCommand("get", "Get a job by id.", out var output);
        var jobId = ActiveCommandFactory.RequiredArgument("jobId", "Job id.");
        var properties = ActiveCommandFactory.StringOption("--properties", "Properties to display.", "--props", "-p");
        var additional = ActiveCommandFactory.StringOption("--additional-properties", "Additional properties to include.", "--add-props");
        ActiveCommandFactory.Add(command, jobId, properties, additional);
        command.SetAction((parse, ct) => ActiveCommandFactory.RunLegacyAsync(parse, output, () =>
            new GetJobsByIdCommand(services).HandleCommand(parse.GetValue(jobId)!, parse.GetValue(properties), parse.GetValue(additional), ct)));
        return command;
    }

    private static Command JobDelete(IServiceProvider services)
    {
        var command = ActiveCommandFactory.NewCommand("delete", "Delete jobs by id.", out var output);
        var ids = ActiveCommandFactory.RequiredManyArgument("ids", "Job ids.");
        var dryRun = ActiveCommandFactory.BoolOption("--dry-run", "Preview job ids without deleting them.");
        var yes = ActiveCommandFactory.BoolOption("--yes", "Confirm deletion without prompting.");
        ActiveCommandFactory.Add(command, ids, dryRun, yes);
        command.SetAction((parse, ct) => ActiveCommandFactory.RunLegacyAsync(parse, output, () =>
            new DeleteJobsByIdCommand(services).HandleCommandWithOptions(parse.GetValue(ids) ?? [], parse.GetValue(dryRun), parse.GetValue(yes), ct)));
        return command;
    }

    private static Command JobSubmit(IServiceProvider services)
    {
        var command = ActiveCommandFactory.NewCommand("submit", "Submit a job definition.", out var output);
        var filePath = ActiveCommandFactory.RequiredArgument("file-path", "Path to JSON job definition.");
        command.Add(filePath);
        command.SetAction((parse, ct) => ActiveCommandFactory.RunLegacyAsync(parse, output, () =>
        {
            var path = parse.GetValue(filePath);
            if (string.Equals(path, "duplicate-merge", StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine("The duplicate-merge job shortcut has been removed from the CLI.");
                return Task.FromResult(CliExitCodes.Usage);
            }

            return new SubmitJobCommand(services).HandleCommand(path!, ct);
        }));
        return command;
    }

    private static Command JobRetry(IServiceProvider services)
    {
        var command = ActiveCommandFactory.NewCommand("retry", "Retry a job.", out var output);
        var jobId = ActiveCommandFactory.RequiredArgument("jobId", "Job id.");
        command.Add(jobId);
        command.SetAction((parse, ct) => ActiveCommandFactory.RunLegacyAsync(parse, output, () =>
            new GetJobsRetryCommand(services).HandleCommand(parse.GetValue(jobId)!, ct)));
        return command;
    }
}

public sealed class LogsCommand : DataHubTopLevelCommand
{
    public LogsCommand(IServiceProvider serviceProvider) : base("logs", serviceProvider)
    {
        Description = "Query DataHub log entries.";
        Add(LogReadCommand("list", "List log entries.", serviceProvider));
        Add(LogReadCommand("query", "Query log entries.", serviceProvider));
    }

    private static Command LogReadCommand(string name, string description, IServiceProvider serviceProvider)
    {
        var command = ActiveCommandFactory.NewCommand(name, description, out var output);
        var where = ActiveCommandFactory.OptionalArgument("where", "Optional log query filter.");
        var properties = ActiveCommandFactory.StringOption("--properties", "Properties to display.", "--props", "-p");
        var additional = ActiveCommandFactory.StringOption("--additional-properties", "Additional properties to include.", "--add-props");
        var dontOpen = ActiveCommandFactory.BoolOption("--dont-open", "Do not open saved output.", "--no");
        var info = ActiveCommandFactory.BoolOption("--info", "Show log information.", "-i");
        var pageSize = ActiveCommandFactory.IntOption("--page-size", "Page size.", "--page");
        var saveTo = ActiveCommandFactory.StringOption("--save-to", "Save results to a file.", "-s");
        ActiveCommandFactory.Add(command, where, properties, additional, dontOpen, info, pageSize, saveTo);
        command.SetAction((parse, ct) => ActiveCommandFactory.RunLegacyAsync(parse, output, () =>
            new GetLogsWhereCommand(serviceProvider).HandleCommand(parse.GetValue(where), parse.GetValue(properties), parse.GetValue(additional), parse.GetValue(dontOpen), parse.GetValue(info), parse.GetValue(pageSize), parse.GetValue(saveTo) ?? string.Empty, ct)));
        return command;
    }
}

public sealed class LocksCommand : DataHubTopLevelCommand
{
    public LocksCommand(IServiceProvider serviceProvider) : base("locks", serviceProvider)
    {
        Description = "Inspect processing locks.";
        var command = ActiveCommandFactory.NewCommand("list", "List processing locks.", out var output);
        command.SetAction((parse, ct) => ActiveCommandFactory.RunLegacyAsync(parse, output, () => new GetLocksCommand(serviceProvider).HandleCommand(ct)));
        Add(command);
    }
}

public sealed class AlertsCommand : DataHubTopLevelCommand
{
    public AlertsCommand(IServiceProvider serviceProvider) : base("alerts", serviceProvider)
    {
        Description = "Inspect DataHub alerts.";
        Add(AlertsList(serviceProvider));
        Add(AlertsGet(serviceProvider));
    }

    private static Command AlertsList(IServiceProvider services)
    {
        var command = ActiveCommandFactory.NewCommand("list", "List alerts by query.", out var output);
        var where = ActiveCommandFactory.OptionalArgument("where", "Optional alert query filter.");
        var properties = ActiveCommandFactory.StringOption("--properties", "Properties to display.", "--props", "-p");
        var additional = ActiveCommandFactory.StringOption("--additional-properties", "Additional properties to include.", "--add-props");
        var pageSize = ActiveCommandFactory.IntOption("--page-size", "Page size.", "--page");
        var info = ActiveCommandFactory.BoolOption("--info", "Show alert count information.", "-i");
        var saveTo = ActiveCommandFactory.StringOption("--save-to", "Save results to a file.", "-s");
        var dontOpen = ActiveCommandFactory.BoolOption("--dont-open", "Do not open saved output.", "--no");
        ActiveCommandFactory.Add(command, where, properties, additional, pageSize, info, saveTo, dontOpen);
        command.SetAction((parse, ct) => ActiveCommandFactory.RunLegacyAsync(parse, output, () =>
            new GetAlertsWhereCommand(services).HandleCommand(parse.GetValue(where), parse.GetValue(properties), parse.GetValue(additional), parse.GetValue(pageSize), parse.GetValue(info), parse.GetValue(saveTo) ?? string.Empty, parse.GetValue(dontOpen), ct)));
        return command;
    }

    private static Command AlertsGet(IServiceProvider services)
    {
        var command = ActiveCommandFactory.NewCommand("get", "Get alerts by id.", out var output);
        var ids = ActiveCommandFactory.RequiredManyArgument("ids", "Alert ids.");
        var properties = ActiveCommandFactory.StringOption("--properties", "Properties to display.", "--props", "-p");
        var additional = ActiveCommandFactory.StringOption("--additional-properties", "Additional properties to include.", "--add-props");
        ActiveCommandFactory.Add(command, ids, properties, additional);
        command.SetAction((parse, ct) => ActiveCommandFactory.RunLegacyAsync(parse, output, () =>
            new GetAlertsByIdCommand(services).HandleCommand(parse.GetValue(ids) ?? [], parse.GetValue(properties), parse.GetValue(additional), ct)));
        return command;
    }
}

public sealed class AlternateKeysCommand : DataHubTopLevelCommand
{
    public AlternateKeysCommand(IServiceProvider serviceProvider) : base("alternate-keys", serviceProvider)
    {
        Description = "Manage alternate keys.";
        Add(AlternateKeysList(serviceProvider));
        Add(AlternateKeysRegister("register", serviceProvider));
        Add(AlternateKeysRegister("import", serviceProvider));
    }

    private static Command AlternateKeysList(IServiceProvider services)
    {
        var command = ActiveCommandFactory.NewCommand("list", "List alternate keys.", out var output);
        var properties = ActiveCommandFactory.StringOption("--properties", "Properties to display.", "--props", "-p");
        var info = ActiveCommandFactory.BoolOption("--info", "Show entity count information.", "-i");
        var pageSize = ActiveCommandFactory.IntOption("--page-size", "Page size.", "--page");
        var saveTo = ActiveCommandFactory.StringOption("--save-to", "Save results to a file.", "-s");
        var dontOpen = ActiveCommandFactory.BoolOption("--dont-open", "Do not open saved output.", "--no");
        ActiveCommandFactory.Add(command, properties, info, pageSize, saveTo, dontOpen);
        command.SetAction((parse, _) => ActiveCommandFactory.RunLegacyAsync(parse, output, () =>
            new GetAlternateKeysCommand(services).HandleCommand(parse.GetValue(properties), parse.GetValue(pageSize), parse.GetValue(info), parse.GetValue(saveTo) ?? string.Empty, parse.GetValue(dontOpen))));
        return command;
    }

    private static Command AlternateKeysRegister(string name, IServiceProvider services)
    {
        var command = ActiveCommandFactory.FileOperationCommand(name, "Register alternate keys.", out var output);
        command.SetAction((parse, ct) => ActiveCommandFactory.RunLegacyAsync(parse, output, () =>
            new RegisterAlternateKeysCommand(services).HandleCommand(
                parse.GetValue<string>("--source")!,
                parse.GetValue<string?>("--path"),
                parse.GetValue<string?>("--conn"),
                parse.GetValue<string?>("--container"),
                parse.GetValue<string?>("--pattern"),
                parse.GetValue<bool>("--silent"),
                parse.GetValue<bool>("--continue"),
                ct)));
        return command;
    }
}

public sealed class TrackingCommand : DataHubTopLevelCommand
{
    public TrackingCommand(IServiceProvider serviceProvider) : base("tracking", serviceProvider)
    {
        Description = "Inspect and delete tracking data.";
        Add(TrackingList(serviceProvider));
        Add(TrackingDelete(serviceProvider));
    }

    private static Command TrackingList(IServiceProvider services)
    {
        var command = ActiveCommandFactory.NewCommand("list", "List tracking data.", out var output);
        var dataSource = ActiveCommandFactory.RequiredArgument("dataSource", "Data source.");
        var entityType = ActiveCommandFactory.RequiredArgument("entityType", "Entity type.");
        var entityId = ActiveCommandFactory.RequiredArgument("entityId", "Entity id, or '*'.");
        var properties = ActiveCommandFactory.StringOption("--properties", "Properties to display.", "--props", "-p");
        var additional = ActiveCommandFactory.StringOption("--additional-properties", "Additional properties to include.", "--add-props");
        var pageSize = ActiveCommandFactory.IntOption("--page-size", "Page size.", "--page");
        var info = ActiveCommandFactory.BoolOption("--info", "Show tracking information.", "-i");
        ActiveCommandFactory.Add(command, dataSource, entityType, entityId, properties, additional, pageSize, info);
        command.SetAction((parse, _) => ActiveCommandFactory.RunLegacyAsync(parse, output, () =>
            new GetTrackingDataCommand(services).HandleCommand(parse.GetValue(dataSource)!, parse.GetValue(entityType)!, parse.GetValue(entityId)!, parse.GetValue(properties), parse.GetValue(additional), parse.GetValue(pageSize), parse.GetValue(info))));
        return command;
    }

    private static Command TrackingDelete(IServiceProvider services)
    {
        var command = ActiveCommandFactory.NewCommand("delete", "Delete tracking data.", out var output);
        var dataSource = ActiveCommandFactory.RequiredArgument("dataSource", "Data source.");
        var entityType = ActiveCommandFactory.RequiredArgument("entityType", "Entity type.");
        var entityId = ActiveCommandFactory.RequiredArgument("entityId", "Entity id, or '*'.");
        var dryRun = ActiveCommandFactory.BoolOption("--dry-run", "Preview matching tracking entries without deleting them.");
        var yes = ActiveCommandFactory.BoolOption("--yes", "Confirm deletion without prompting.");
        ActiveCommandFactory.Add(command, dataSource, entityType, entityId, dryRun, yes);
        command.SetAction((parse, _) => ActiveCommandFactory.RunLegacyAsync(parse, output, () =>
            new DeleteTrackingDataCommand(services).HandleCommandWithOptions(parse.GetValue(dataSource)!, parse.GetValue(entityType)!, parse.GetValue(entityId)!, parse.GetValue(dryRun), parse.GetValue(yes))));
        return command;
    }
}

internal static class ActiveCommandFactory
{
    public static Command NewCommand(string name, string description, out Option<string?> output, bool includeTenantIdAlias = true)
    {
        var command = new Command(name, description);
        CommandContextOptions.AddTargetOptions(command, includeTenantIdAlias);
        output = OutputOption(command);
        return command;
    }

    public static Command FileOperationCommand(string name, string description, out Option<string?> output)
    {
        var command = new Command(name, description);
        CommandContextOptions.AddTargetOptions(command);
        output = OutputOption(command);
        var source = StringOption("--source", "AZStorage | LocalFile | LocalFolder | AZCosmos");
        source.Required = true;
        Add(command,
            source,
            StringOption("--path", "Path to local file or folder."),
            StringOption("--pattern", "Regular expression pattern for file names to process."),
            BoolOption("--silent", "Do not update last-updated timestamps."),
            BoolOption("--continue", "Continue on failure."),
            StringOption("--conn", "Azure Storage or Azure Cosmos connection string."),
            StringOption("--container", "Azure Storage or Azure Cosmos container name."));
        return command;
    }

    public static Option<string?> OutputOption(Command command)
    {
        var output = new Option<string?>("--output") { Description = "Output format: table, json, ndjson, or tsv." };
        command.Add(output);
        return output;
    }

    public static Argument<string> RequiredArgument(string name, string description)
        => new(name) { Description = description, Arity = ArgumentArity.ExactlyOne };

    public static Argument<string?> OptionalArgument(string name, string description)
        => new(name) { Description = description, Arity = ArgumentArity.ZeroOrOne };

    public static Argument<string[]> RequiredManyArgument(string name, string description)
        => new(name) { Description = description, Arity = ArgumentArity.OneOrMore };

    public static Argument<string[]> OptionalManyArgument(string name, string description)
        => new(name) { Description = description, Arity = ArgumentArity.ZeroOrMore };

    public static Option<string?> StringOption(string name, string description, params string[] aliases)
    {
        var option = new Option<string?>(name) { Description = description };
        foreach (var alias in aliases)
        {
            option.Aliases.Add(alias);
        }
        return option;
    }

    public static Option<int?> IntOption(string name, string description, params string[] aliases)
    {
        var option = new Option<int?>(name) { Description = description };
        foreach (var alias in aliases)
        {
            option.Aliases.Add(alias);
        }
        return option;
    }

    public static Option<bool> BoolOption(string name, string description, params string[] aliases)
    {
        var option = new Option<bool>(name) { Description = description };
        foreach (var alias in aliases)
        {
            option.Aliases.Add(alias);
        }
        return option;
    }

    public static void Add(Command command, params object[] symbols)
    {
        foreach (var symbol in symbols)
        {
            switch (symbol)
            {
                case Argument argument:
                    command.Add(argument);
                    break;
                case Option option:
                    command.Add(option);
                    break;
                case Command subcommand:
                    command.Add(subcommand);
                    break;
                default:
                    throw new ArgumentException($"Unsupported command symbol type '{symbol.GetType().FullName}'.", nameof(symbols));
            }
        }
    }

    public static async Task<int> RunLegacyAsync(ParseResult parseResult, Option<string?> outputOption, Func<Task<int>> action)
    {
        return await RunWithContextAsync(parseResult, outputOption, action, NormalizeLegacyExitCode);
    }

    public static Task<int> RunActiveAsync(ParseResult parseResult, Option<string?> outputOption, Func<Task<int>> action)
    {
        return RunWithContextAsync(parseResult, outputOption, action, exitCode => exitCode);
    }

    private static async Task<int> RunWithContextAsync(ParseResult parseResult, Option<string?> outputOption, Func<Task<int>> action, Func<int, int> normalizeExitCode)
    {
        var previousTarget = CliTargetContext.Current;
        var previousOutputFormat = CliOutputContext.Format;
        CliTargetContext.Current = CommandContextOptions.CreateTargetOptions(parseResult);
        CliOutputContext.Format = CommandContextOptions.ParseOutputFormat(parseResult.GetValue(outputOption));
        try
        {
            return normalizeExitCode(await action());
        }
        catch (OperationCanceledException)
        {
            return CliExitCodes.Cancelled;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Error: {exception.Message}");
            return CliExitCodes.Failure;
        }
        finally
        {
            CliTargetContext.Current = previousTarget;
            CliOutputContext.Format = previousOutputFormat;
        }
    }

    private static int NormalizeLegacyExitCode(int exitCode)
        => exitCode switch
        {
            CliExitCodes.LegacyRuntimeFailure => CliExitCodes.Failure,
            1 => CliExitCodes.Success,
            0 => CliExitCodes.Failure,
            130 => CliExitCodes.Cancelled,
            _ => exitCode
        };
}
