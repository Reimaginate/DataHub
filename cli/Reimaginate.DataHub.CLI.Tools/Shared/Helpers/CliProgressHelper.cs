using Reimaginate.DataHub.CLI.Tools.Shared.Runtime;
using Spectre.Console;

namespace Reimaginate.DataHub.CLI.Tools.Shared.Helpers;

internal static class CliProgressHelper
{
    public static bool ShouldRender => CliOutputContext.Format == CliOutputFormat.Table;

    public static Task RunAsync(Func<CliProgressContext, Task> action)
    {
        if (!ShouldRender)
        {
            return action(new CliProgressContext(null));
        }

        return AnsiConsole
            .Progress()
            .Columns(
            [
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn()
            ])
            .StartAsync(ctx => action(new CliProgressContext(ctx)));
    }

    public static async Task<T> RunAsync<T>(Func<CliProgressContext, Task<T>> action)
    {
        if (!ShouldRender)
        {
            return await action(new CliProgressContext(null));
        }

        T? result = default;
        await AnsiConsole
            .Progress()
            .Columns(
            [
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn()
            ])
            .StartAsync(async ctx =>
            {
                result = await action(new CliProgressContext(ctx));
            });

        return result!;
    }

    public static Task StatusAsync(string status, Func<Task> action)
    {
        if (!ShouldRender)
        {
            return action();
        }

        return AnsiConsole
            .Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync(Escape(status), _ => action());
    }

    public static async Task<T> StatusAsync<T>(string status, Func<Task<T>> action)
    {
        if (!ShouldRender)
        {
            return await action();
        }

        T? result = default;
        await AnsiConsole
            .Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync(Escape(status), async _ =>
            {
                result = await action();
            });

        return result!;
    }

    internal static string Escape(string value)
        => (value ?? string.Empty).EscapeMarkup();
}

internal sealed class CliProgressContext(ProgressContext? context)
{
    public CliProgressTask AddTask(string description, double maxValue = 100)
        => new(context?.AddTask(CliProgressHelper.Escape(description), maxValue: maxValue));
}

internal sealed class CliProgressTask(ProgressTask? task)
{
    public string Description
    {
        get => task?.Description ?? string.Empty;
        set
        {
            if (task != null)
            {
                task.Description = CliProgressHelper.Escape(value);
            }
        }
    }

    public bool IsIndeterminate
    {
        get => task?.IsIndeterminate ?? false;
        set
        {
            if (task != null)
            {
                task.IsIndeterminate = value;
            }
        }
    }

    public double Value
    {
        get => task?.Value ?? 0;
        set
        {
            if (task != null)
            {
                task.Value = value;
            }
        }
    }

    public double MaxValue
    {
        get => task?.MaxValue ?? 0;
        set
        {
            if (task != null)
            {
                task.MaxValue = value;
            }
        }
    }

    public void Increment(double value)
        => task?.Increment(value);

    public void StartTask()
        => task?.StartTask();

    public void StopTask()
        => task?.StopTask();
}
