using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Reimaginate.CLI.Base.Config;
using Reimaginate.CLI.Base.Dynamic;
using Reimaginate.DataHub.CLI;
using Reimaginate.DataHub.CLI.Tools;
using Reimaginate.Mediator;
using System.CommandLine;
using System.IO.Compression;
using System.Xml.Linq;
using Xunit;

namespace Reimaginate.DataHub.CLI.Test.Unit;

public class HostProjectBoundaryTests
{
#if DEBUG
    private const string BuildConfiguration = "Debug";
#else
    private const string BuildConfiguration = "Release";
#endif

    [Fact]
    public void Executable_host_does_not_contain_tools_implementation_folders()
    {
        var repositoryLayout = FindRepositoryLayout();
        var hostProjectPath = repositoryLayout.HostProjectDirectory;

        Directory.Exists(Path.Combine(hostProjectPath, "Commands")).Should().BeFalse();
        Directory.Exists(Path.Combine(hostProjectPath, "Shared")).Should().BeFalse();
        Directory.Exists(Path.Combine(hostProjectPath, "Helpers")).Should().BeFalse();
        Directory.Exists(Path.Combine(hostProjectPath, "PluginBase")).Should().BeFalse();
    }

    [Fact]
    public void Executable_host_references_tools_project_for_build_order_only()
    {
        var repositoryLayout = FindRepositoryLayout();
        var hostProjectFile = Path.Combine(repositoryLayout.HostProjectDirectory, "Reimaginate.DataHub.CLI.csproj");
        var project = XDocument.Load(hostProjectFile);
        var toolsReference = project.Descendants("ProjectReference")
            .Single(element => element.Attribute("Include")?.Value.Contains("Reimaginate.DataHub.CLI.Tools.csproj") == true);

        toolsReference.Attribute("ReferenceOutputAssembly")?.Value.Should().Be("false");
        toolsReference.Attribute("PrivateAssets")?.Value.Should().Be("all");
    }

    [Fact]
    public void Dynamic_module_registration_exposes_base_tools_and_datahub_commands()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "datahub-cli-host-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var config = new ConfigurationBuilder().Build();
            var options = new CliToolRuntimeOptions
            {
                RootDirectory = tempRoot,
                BundledToolsDirectory = Path.Combine(tempRoot, "bundled-tools")
            };
            var services = new ServiceCollection()
                .AddSingleton(options)
                .AddSingleton<ICliToolManifestStore>(new CliToolManifestStore(options))
                .AddSingleton<CliToolRuntime>()
                .AddSingleton<IMediator, ReflectionMediator>();
            services.AddBaseCommands(config);
            var module = new CliToolModule();
            module.ConfigureServices(services, config);
            using var serviceProvider = services.BuildServiceProvider();
            var rootCommand = new RootCommand();

            rootCommand.AddBaseCommands(serviceProvider);
            module.ConfigureCommands(rootCommand, serviceProvider);

            rootCommand.Subcommands.Select(command => command.Name).Should().Contain(["tools", "profiles", "auth", "entities", "data", "export", "logs", "login", "logout", "whoami"]);
            rootCommand.Subcommands.Select(command => command.Name).Should().NotContain(["users", "roles", "permissions", "export-data"]);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                try
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
                catch (UnauthorizedAccessException)
                {
                }
                catch (IOException)
                {
                }
            }
        }
    }

    [Fact]
    public async Task Bundled_package_runtime_exposes_trimmed_datahub_command_tree()
    {
        var repositoryLayout = FindRepositoryLayout();
        var tempRoot = Path.Combine(Path.GetTempPath(), "datahub-cli-runtime-test-" + Guid.NewGuid().ToString("N"));
        var bundledToolsDirectory = Path.Combine(tempRoot, "bundled-tools");
        try
        {
            Directory.CreateDirectory(bundledToolsDirectory);
            var version = GetPackageVersion(repositoryLayout.RootDirectory, "Reimaginate.DataHub.CLI.Tools");
            await StageBundledToolPackages(repositoryLayout, bundledToolsDirectory, version);
            var noticesPath = Path.Combine(bundledToolsDirectory, "THIRD-PARTY-NOTICES.txt");
            File.Exists(noticesPath).Should().BeTrue();
            var notices = File.ReadAllText(noticesPath);
            notices.Should().Contain("DATAHUB CLI THIRD-PARTY NOTICES");
            notices.Should().Contain("Package: OneOf");
            notices.Should().Contain("Version: 3.0.271");
            notices.Should().Contain("Copyright (c) 2016 Harry McIntyre");
            notices.Should().Contain("Package: CsvHelper");
            notices.Should().Contain("Selected distribution licence: Apache-2.0");
            notices.Should().Contain("Package: Reimaginate.CLI.Base");
            notices.Should().Contain("Copyright (c) 2016 ClosedXML");
            notices.Should().Contain("Copyright (c) 2023, Jan Havlíček");
            notices.Should().Contain("Copyright (c) 2017 andersnm");
            notices.Should().Contain("UPSTREAM THIRD-PARTY NOTICE");
            notices.Should().Contain("License notice for Unicode data");
            notices.Should().NotContain("Package: Reimaginate.DataHub.");
            RestoreBundledToolDependencies(tempRoot, bundledToolsDirectory);

            var config = new ConfigurationBuilder().Build();
            var options = new CliToolRuntimeOptions
            {
                RootDirectory = tempRoot,
                BundledToolsDirectory = bundledToolsDirectory
            };
            var manifestStore = new CliToolManifestStore(options);
            var runtime = new CliToolRuntime(options, manifestStore);
            var modules = await runtime.LoadModulesAsync(CancellationToken.None);
            var services = new ServiceCollection()
                .AddSingleton(options)
                .AddSingleton<ICliToolManifestStore>(manifestStore)
                .AddSingleton(runtime)
                .AddSingleton<IMediator, ReflectionMediator>();
            services.AddBaseCommands(config);

            foreach (var module in modules)
            {
                module.Module.ConfigureServices(services, config);
            }

            using var serviceProvider = services.BuildServiceProvider();
            var rootCommand = new RootCommand();
            rootCommand.AddBaseCommands(serviceProvider);

            foreach (var module in modules)
            {
                module.Module.ConfigureCommands(rootCommand, serviceProvider);
            }

            var trimmed = TrimForDataHubHost(rootCommand);

            trimmed.Subcommands.Select(command => command.Name).Should().Contain([
                "tools",
                "profiles",
                "auth",
                "entities",
                "data",
                "export",
                "jobs",
                "logs",
                "alternate-keys",
                "tracking"
            ]);
            trimmed.Subcommands.Select(command => command.Name).Should().NotContain(["users", "roles", "permissions", "sync", "merge", "patch", "export-data"]);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                try
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
                catch (UnauthorizedAccessException)
                {
                }
                catch (IOException)
                {
                }
            }
        }
    }

    [Fact]
    public void Executable_host_packs_bundled_tool_and_dependency_packages()
    {
        var repositoryLayout = FindRepositoryLayout();
        var hostProjectFile = Path.Combine(repositoryLayout.HostProjectDirectory, "Reimaginate.DataHub.CLI.csproj");
        var projectText = File.ReadAllText(hostProjectFile);

        projectText.Should().Contain("<NoDefaultExcludes>true</NoDefaultExcludes>");
        projectText.Should().Contain("tools/$(TargetFramework)/any/bundled-tools/");
        projectText.Should().Contain("bundled-tools\\dependencies");
        projectText.Should().Contain("PackagePath=\"THIRD-PARTY-NOTICES.txt\"");
        projectText.Should().NotContain("bundled-tool-dependencies");
    }

    [Fact]
    public void DataHub_cli_tool_root_is_stable_across_install_paths()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "datahub-cli-storage-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var first = DataHubCliToolStoragePaths.CreateRuntimeOptions(
                Path.Combine(tempRoot, "install-a"),
                Path.Combine(tempRoot, "local-app-data"),
                Path.Combine(tempRoot, "temp"),
                Path.Combine(tempRoot, "current"));
            var second = DataHubCliToolStoragePaths.CreateRuntimeOptions(
                Path.Combine(tempRoot, "install-b"),
                Path.Combine(tempRoot, "local-app-data"),
                Path.Combine(tempRoot, "temp"),
                Path.Combine(tempRoot, "current"));

            second.RootDirectory.Should().Be(first.RootDirectory);
            first.RootDirectory.Should().EndWith(Path.Combine("hosts", DataHubCliToolStoragePaths.StableHostId));
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public void DataHub_cli_tool_root_ignores_bundled_package_content_changes()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "datahub-cli-storage-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var firstInstall = Path.Combine(tempRoot, "install-a");
            var secondInstall = Path.Combine(tempRoot, "install-b");
            Directory.CreateDirectory(Path.Combine(firstInstall, "bundled-tools"));
            Directory.CreateDirectory(Path.Combine(secondInstall, "bundled-tools"));
            File.WriteAllText(Path.Combine(firstInstall, "bundled-tools", "Reimaginate.DataHub.CLI.Tools.1.0.0.nupkg"), "old package bytes");
            File.WriteAllText(Path.Combine(secondInstall, "bundled-tools", "Reimaginate.DataHub.CLI.Tools.1.0.1.nupkg"), "new package bytes");

            var first = DataHubCliToolStoragePaths.CreateRuntimeOptions(
                firstInstall,
                Path.Combine(tempRoot, "local-app-data"),
                Path.Combine(tempRoot, "temp"),
                Path.Combine(tempRoot, "current"));
            var second = DataHubCliToolStoragePaths.CreateRuntimeOptions(
                secondInstall,
                Path.Combine(tempRoot, "local-app-data"),
                Path.Combine(tempRoot, "temp"),
                Path.Combine(tempRoot, "current"));

            second.RootDirectory.Should().Be(first.RootDirectory);
            second.BundledToolsDirectory.Should().NotBe(first.BundledToolsDirectory);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public async Task DataHub_cli_tool_root_migrates_newest_legacy_host_manifest()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "datahub-cli-storage-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var localAppData = Path.Combine(tempRoot, "local-app-data");
            var hostsRoot = Path.Combine(localAppData, "Reimaginate", "DataHub", "CLI", "tools", "hosts");
            var olderLegacyRoot = Path.Combine(hostsRoot, "1111111111111111");
            var newestLegacyRoot = Path.Combine(hostsRoot, "2222222222222222");
            await WriteLegacyToolManifest(olderLegacyRoot, "Older.Sample.Tool", "1.0.0", DateTime.UtcNow.AddDays(-1));
            await WriteLegacyToolManifest(newestLegacyRoot, "Migrated.Sample.Tool", "1.0.0", DateTime.UtcNow);
            CopyAssemblyToPackage(newestLegacyRoot, "Migrated.Sample.Tool", "1.0.0", typeof(CliToolRuntime).Assembly.Location);

            var options = DataHubCliToolStoragePaths.CreateRuntimeOptions(
                Path.Combine(tempRoot, "install"),
                localAppData,
                Path.Combine(tempRoot, "temp"),
                Path.Combine(tempRoot, "current"));
            var manifestStore = new CliToolManifestStore(options);
            var manifest = await manifestStore.ReadAsync(CancellationToken.None);
            var runtime = new CliToolRuntime(options, manifestStore);
            var modules = await runtime.LoadInstalledModulesAsync(CancellationToken.None);

            options.LegacyRootDirectories.First().Should().Be(newestLegacyRoot);
            manifest.Tools.Should().ContainSingle(tool => tool.PackageId == "Migrated.Sample.Tool");
            File.Exists(Path.Combine(options.RootDirectory, "tools.json")).Should().BeTrue();
            File.Exists(Path.Combine(options.RootDirectory, "packages", "migrated.sample.tool", "1.0.0", "lib", "net9.0", Path.GetFileName(typeof(CliToolRuntime).Assembly.Location))).Should().BeTrue();
            modules.Should().Contain(module => module.Module.PackageId == "Migrated.Sample.Tool");
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static async Task WriteLegacyToolManifest(string rootDirectory, string packageId, string version, DateTime manifestLastWriteTimeUtc)
    {
        Directory.CreateDirectory(rootDirectory);
        var manifestPath = Path.Combine(rootDirectory, "tools.json");
        await File.WriteAllTextAsync(
            manifestPath,
            $$"""
            {
              "tools": [
                {
                  "packageId": "{{packageId}}",
                  "version": "{{version}}",
                  "source": "C:\\packages",
                  "installedAt": "2026-01-01T00:00:00+00:00",
                  "dependencies": []
                }
              ]
            }
            """);
        File.SetLastWriteTimeUtc(manifestPath, manifestLastWriteTimeUtc);
    }

    private static void CopyAssemblyToPackage(string rootDirectory, string packageId, string version, string assemblyPath)
    {
        var packageDirectory = Path.Combine(
            rootDirectory,
            "packages",
            packageId.ToLowerInvariant(),
            version.ToLowerInvariant(),
            "lib",
            "net9.0");

        Directory.CreateDirectory(packageDirectory);
        File.Copy(assemblyPath, Path.Combine(packageDirectory, Path.GetFileName(assemblyPath)));
        File.WriteAllText(Path.Combine(rootDirectory, "packages", packageId.ToLowerInvariant(), version.ToLowerInvariant(), packageId + ".nuspec"), "<package />");
    }

    private static void TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (IOException)
        {
        }
    }

    private static RepositoryLayout FindRepositoryLayout()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var privateHostProjectDirectory = Path.Combine(
                directory.FullName,
                "src",
                "Reimaginate.DataHub.CLI");
            var privateBundledToolsScriptPath = Path.Combine(
                directory.FullName,
                "tools",
                "stage-cli-bundled-tools.ps1");
            if (
                File.Exists(Path.Combine(directory.FullName, "Reimaginate.DataHub.sln")) &&
                File.Exists(Path.Combine(
                    privateHostProjectDirectory,
                    "Reimaginate.DataHub.CLI.csproj")) &&
                File.Exists(privateBundledToolsScriptPath)
            )
            {
                return new RepositoryLayout(
                    directory.FullName,
                    privateHostProjectDirectory,
                    privateBundledToolsScriptPath);
            }

            var publicHostProjectDirectory = Path.Combine(
                directory.FullName,
                "cli",
                "Reimaginate.DataHub.CLI");
            var publicBundledToolsScriptPath = Path.Combine(
                directory.FullName,
                "cli",
                "tools",
                "stage-cli-bundled-tools.ps1");
            if (
                File.Exists(Path.Combine(directory.FullName, "Reimaginate.DataHub.slnx")) &&
                File.Exists(Path.Combine(
                    publicHostProjectDirectory,
                    "Reimaginate.DataHub.CLI.csproj")) &&
                File.Exists(publicBundledToolsScriptPath)
            )
            {
                return new RepositoryLayout(
                    directory.FullName,
                    publicHostProjectDirectory,
                    publicBundledToolsScriptPath);
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate a supported private or public repository layout.");
    }

    private static RootCommand TrimForDataHubHost(RootCommand command)
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

    private static async Task StageBundledToolPackages(
        RepositoryLayout repositoryLayout,
        string bundledToolsDirectory,
        string version)
    {
        var processStartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "pwsh",
            WorkingDirectory = repositoryLayout.RootDirectory,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        processStartInfo.ArgumentList.Add("-NoProfile");
        processStartInfo.ArgumentList.Add("-ExecutionPolicy");
        processStartInfo.ArgumentList.Add("Bypass");
        processStartInfo.ArgumentList.Add("-File");
        processStartInfo.ArgumentList.Add(repositoryLayout.BundledToolsScriptPath);
        processStartInfo.ArgumentList.Add("-Configuration");
        processStartInfo.ArgumentList.Add(BuildConfiguration);
        processStartInfo.ArgumentList.Add("-Version");
        processStartInfo.ArgumentList.Add(version);
        processStartInfo.ArgumentList.Add("-OutputPath");
        processStartInfo.ArgumentList.Add(bundledToolsDirectory);
        processStartInfo.ArgumentList.Add("-DependencyOutputPath");
        processStartInfo.ArgumentList.Add(Path.Combine(bundledToolsDirectory, "dependencies"));
        processStartInfo.ArgumentList.Add("-ArtifactsPath");
        processStartInfo.ArgumentList.Add(Path.Combine(Path.GetTempPath(), "datahub-cli-runtime-test-packages-" + Guid.NewGuid().ToString("N")));
        processStartInfo.ArgumentList.Add("-NoBuild");

        using var process = System.Diagnostics.Process.Start(processStartInfo)
                            ?? throw new InvalidOperationException("Could not start bundled tool package staging.");
        var output = await process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var error = await process.StandardError.ReadToEndAsync(CancellationToken.None);
        await process.WaitForExitAsync(CancellationToken.None);

        process.ExitCode.Should().Be(0, output + error);
    }

    private sealed record RepositoryLayout(
        string RootDirectory,
        string HostProjectDirectory,
        string BundledToolsScriptPath);

    private static void RestoreBundledToolDependencies(string rootDirectory, string bundledToolsDirectory)
    {
        var dependenciesDirectory = Path.Combine(bundledToolsDirectory, "dependencies");
        var packageRoot = Path.Combine(rootDirectory, "packages");
        Directory.CreateDirectory(packageRoot);

        foreach (var packageFilePath in Directory.GetFiles(dependenciesDirectory, "*.nupkg", SearchOption.TopDirectoryOnly))
        {
            var identity = ReadPackageIdentity(packageFilePath);
            var packageDirectory = Path.Combine(
                packageRoot,
                identity.PackageId.ToLowerInvariant(),
                identity.Version.ToLowerInvariant());

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
        }
    }

    private static (string PackageId, string Version) ReadPackageIdentity(string packageFilePath)
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

    private static string GetPackageVersion(string repositoryRoot, string packageId)
    {
        var versionsFilePath = Path.Combine(repositoryRoot, "release", "PackageVersions.props");
        var document = XDocument.Load(versionsFilePath);
        return document
                   .Descendants("DataHubPublicPackage")
                   .Single(element => element.Attribute("Include")?.Value == packageId)
                   .Element("Version")
                   ?.Value
               ?? throw new InvalidOperationException($"Could not find package version for '{packageId}'.");
    }
}
