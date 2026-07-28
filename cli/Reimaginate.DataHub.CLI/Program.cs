using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Reimaginate.CLI.Base.Config;
using Reimaginate.CLI.Base.Dynamic;
using Reimaginate.CLI.Base.Helpers;
using Reimaginate.Mediator;
using Reimaginate.DataHub.CLI;
using System.Security.Cryptography;
using System.CommandLine;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

var processedStdinArgs = ProcessRedirectedInput(args);
using var shutdown = new CancellationTokenSource();
ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
{
    if (!shutdown.IsCancellationRequested)
    {
        eventArgs.Cancel = true;
        Console.Error.WriteLine("Cancellation requested. Waiting for the current operation to stop...");
        shutdown.Cancel();
        return;
    }

    eventArgs.Cancel = false;
};
Console.CancelKeyPress += cancelHandler;

var cliToolOptions = DataHubCliToolStoragePaths.CreateRuntimeOptions(AppContext.BaseDirectory);
RestoreBundledToolDependencies(cliToolOptions);
var cliToolManifestStore = new CliToolManifestStore(cliToolOptions);
var cliToolRuntime = new CliToolRuntime(cliToolOptions, cliToolManifestStore);
var dynamicToolModules = await cliToolRuntime.LoadModulesAsync();

var builder = Host.CreateDefaultBuilder(processedStdinArgs)
    .ConfigureAppConfiguration((_, configuration) =>
    {
        configuration.Sources.Clear();
        configuration.AddEnvironmentVariables();
        configuration.AddJsonFile("appsettings.json", optional: true);
    })
    .ConfigureServices((hostContext, services) =>
    {
        services.AddSingleton(cliToolOptions);
        services.AddSingleton<ICliToolManifestStore>(cliToolManifestStore);
        services.AddSingleton(cliToolRuntime);
        services.AddSingleton<IMediator, ReflectionMediator>();
        services.AddBaseCommands(hostContext.Configuration);

        foreach (var loadedModule in dynamicToolModules)
        {
            loadedModule.Module.ConfigureServices(services, hostContext.Configuration);
        }
    })
    .ConfigureLogging(logging => logging.ClearProviders());

using var app = builder.Build();
var appStarted = false;
try
{
    WriteAuthDebug("host starting.");
    await app.StartAsync(shutdown.Token);
    appStarted = true;
    WriteAuthDebug("host started.");

    var rootCommand = new RootCommand("DataHub command line tools");
    rootCommand.AddBaseCommands(app.Services);

    foreach (var loadedModule in dynamicToolModules)
    {
        loadedModule.Module.ConfigureCommands(rootCommand, app.Services);
    }

    var finalArgs = await ArgumentsPreprocessor.ReplaceTokens(processedStdinArgs);
    WriteAuthDebug("command invocation starting.");
    var exitCode = await TrimDataHubRootCommand(rootCommand).Parse(finalArgs).InvokeAsync(new InvocationConfiguration(), shutdown.Token);
    WriteAuthDebug($"command invocation returned exit code {exitCode}.");
    return exitCode;
}
catch (OperationCanceledException)
{
    WriteAuthDebug("command invocation cancelled.");
    return 130;
}
finally
{
    Console.CancelKeyPress -= cancelHandler;
    if (appStarted)
    {
        WriteAuthDebug("host stopping.");
        await app.StopAsync(CancellationToken.None);
        WriteAuthDebug("host stopped.");
    }
}

static string[] ProcessRedirectedInput(string[] args)
{
    var extArgs = new List<string>(args);

    if (!Console.IsInputRedirected)
    {
        return extArgs.ToArray();
    }

    var input = Console.ReadLine();
    if (string.IsNullOrEmpty(input))
    {
        return extArgs.ToArray();
    }

    var inputs = new List<List<string>> { input.Split('\t').ToList() };
    while (Console.In.Peek() != -1)
    {
        input = Console.ReadLine();
        if (!string.IsNullOrEmpty(input))
        {
            inputs.Add(input.Split('\t').ToList());
        }
    }

    for (var index = 0; index < extArgs.Count; index++)
    {
        var arg = extArgs[index];
        if (!arg.Trim().StartsWith("$", StringComparison.Ordinal))
        {
            continue;
        }

        var inputIndex = int.TryParse(arg.Trim().Replace("$", string.Empty), out var parsed)
            ? parsed
            : throw new InvalidOperationException("Invalid redirected input argument.");

        var values = inputs.Select(row => row[inputIndex]).ToList();
        extArgs.Insert(index, string.Join(" ", values));
        extArgs.Remove(arg);
    }

    if (!Console.IsOutputRedirected)
    {
        Console.WriteLine("DataHub " + string.Join(" ", extArgs.Take(5)) + (extArgs.Count > 5 ? "..." : string.Empty));
        Console.WriteLine();
    }

    return extArgs.ToArray();
}

static void WriteAuthDebug(string message)
{
    var value = Environment.GetEnvironmentVariable("DATAHUB_AUTH_DEBUG");
    if (!string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase))
    {
        return;
    }

    Console.Error.WriteLine($"[datahub-auth] {DateTimeOffset.Now:O} {message}");
}

static RootCommand TrimDataHubRootCommand(RootCommand command)
{
    var trimmed = new RootCommand(command.Description ?? string.Empty);

    foreach (var argument in command.Arguments)
    {
        trimmed.Add(argument);
    }

    foreach (var subcommand in command.Subcommands)
    {
        if (subcommand.Subcommands.Any() || subcommand.Action is not null)
        {
            trimmed.Add(subcommand);
        }
    }

    return trimmed;
}

static void RestoreBundledToolDependencies(CliToolRuntimeOptions options)
{
    var dependenciesDirectory = Path.Combine(AppContext.BaseDirectory, "bundled-tools", "dependencies");
    if (!Directory.Exists(dependenciesDirectory))
    {
        return;
    }

    var mutexName = @"Global\ReimaginateDataHubCliRestore_" +
                    Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(options.RootDirectory)))[..16];
    using var restoreMutex = new Mutex(false, mutexName);
    var lockTaken = restoreMutex.WaitOne(TimeSpan.FromMinutes(5));
    if (!lockTaken)
    {
        throw new TimeoutException("Timed out waiting for the DataHub CLI dependency restore lock.");
    }

    try
    {
        var packageRoot = Path.Combine(options.RootDirectory, "packages");
        Directory.CreateDirectory(packageRoot);

        foreach (var packageFilePath in Directory.GetFiles(dependenciesDirectory, "*.nupkg", SearchOption.TopDirectoryOnly))
        {
            var identity = ReadPackageIdentity(packageFilePath);
            var packageDirectory = Path.Combine(
                packageRoot,
                identity.PackageId.ToLowerInvariant(),
                identity.Version.ToLowerInvariant());

            if (Directory.Exists(packageDirectory)
                && Directory.GetFiles(packageDirectory, "*.nuspec").Any()
                && Directory.GetLastWriteTimeUtc(packageDirectory) >= File.GetLastWriteTimeUtc(packageFilePath))
            {
                continue;
            }

            Directory.CreateDirectory(packageDirectory);
            using var archive = ZipFile.OpenRead(packageFilePath);
            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Name))
                {
                    continue;
                }

                var destinationPath = Path.Combine(packageDirectory, entry.FullName);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                entry.ExtractToFile(destinationPath, overwrite: true);
            }

            Directory.SetLastWriteTimeUtc(packageDirectory, File.GetLastWriteTimeUtc(packageFilePath));
        }
    }
    finally
    {
        restoreMutex.ReleaseMutex();
    }
}

static (string PackageId, string Version) ReadPackageIdentity(string packageFilePath)
{
    using var archive = ZipFile.OpenRead(packageFilePath);
    var nuspecEntry = archive.Entries.FirstOrDefault(entry => entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase))
                      ?? throw new InvalidOperationException($"Package '{packageFilePath}' does not contain a nuspec.");
    using var nuspecStream = nuspecEntry.Open();
    var nuspec = XDocument.Load(nuspecStream);
    var metadata = nuspec.Root?.Elements().FirstOrDefault(element => element.Name.LocalName == "metadata")
                   ?? throw new InvalidOperationException($"Package '{packageFilePath}' does not contain nuspec metadata.");
    var id = metadata.Elements().FirstOrDefault(element => element.Name.LocalName == "id")?.Value;
    var version = metadata.Elements().FirstOrDefault(element => element.Name.LocalName == "version")?.Value;

    if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(version))
    {
        throw new InvalidOperationException($"Package '{packageFilePath}' does not contain a valid id and version.");
    }

    return (id, version);
}
