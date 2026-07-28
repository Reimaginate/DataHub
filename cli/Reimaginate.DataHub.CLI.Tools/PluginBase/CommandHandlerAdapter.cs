using System.CommandLine;
using System.Reflection;
using Reimaginate.DataHub.CLI.Tools.Shared.Runtime;

namespace Reimaginate.DataHub.CLI.Tools.PluginBase;

internal static class CommandHandlerAdapter
{
    public static async Task<int> InvokeAsync(Command command, Delegate handler, ParseResult parseResult, CancellationToken cancellationToken)
    {
        var parameters = handler.Method.GetParameters();
        var values = new object?[parameters.Length];

        for (var index = 0; index < parameters.Length; index++)
        {
            var parameter = parameters[index];
            values[index] = ResolveParameter(command, parseResult, parameter, cancellationToken);
        }

        try
        {
            var result = handler.DynamicInvoke(values);
            if (result is Task<int> intTask)
            {
                return await intTask;
            }

            if (result is Task task)
            {
                await task;
                return 0;
            }

            return result is int exitCode ? exitCode : 0;
        }
        catch (Exception exception)
        {
            var error = exception is TargetInvocationException { InnerException: not null }
                ? exception.InnerException
                : exception;

            Console.Error.WriteLine($"Error: {error.Message}");
            return CliExitCodes.LegacyRuntimeFailure;
        }
    }

    private static object? ResolveParameter(Command command, ParseResult parseResult, ParameterInfo parameter, CancellationToken cancellationToken)
    {
        if (parameter.ParameterType == typeof(CancellationToken))
        {
            return cancellationToken;
        }

        var symbolName = FindSymbolName(command, parameter.Name ?? string.Empty);
        if (symbolName != null)
        {
            var getValue = typeof(ParseResult)
                .GetMethods()
                .Single(method => method.Name == nameof(ParseResult.GetValue) &&
                                  method.IsGenericMethodDefinition &&
                                  method.GetParameters().Length == 1 &&
                                  method.GetParameters()[0].ParameterType == typeof(string))
                .MakeGenericMethod(parameter.ParameterType);

            var value = getValue.Invoke(parseResult, [symbolName]);
            if (value != null)
            {
                return value;
            }
        }

        return parameter.HasDefaultValue ? parameter.DefaultValue : GetDefault(parameter.ParameterType);
    }

    private static string? FindSymbolName(Command command, string parameterName)
    {
        var normalizedParameterName = Normalize(parameterName);

        var candidate = command.Options.FirstOrDefault(option =>
            option.Aliases.Any(alias => Normalize(alias) == normalizedParameterName) ||
            Normalize(option.Name) == normalizedParameterName);
        if (candidate != null)
        {
            return candidate.Name;
        }

        var argument = command.Arguments.FirstOrDefault(argument => Normalize(argument.Name) == normalizedParameterName);
        return argument?.Name;
    }

    private static string Normalize(string value)
    {
        return value
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .TrimStart('-')
            .ToLowerInvariant();
    }

    private static object? GetDefault(Type type)
    {
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}
