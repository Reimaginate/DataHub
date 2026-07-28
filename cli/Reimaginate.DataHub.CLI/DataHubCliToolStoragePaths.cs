using Reimaginate.CLI.Base.Dynamic;

namespace Reimaginate.DataHub.CLI;

public static class DataHubCliToolStoragePaths
{
    public const string StableHostId = "reimaginate-datahub-cli";

    public static CliToolRuntimeOptions CreateRuntimeOptions(string appBaseDirectory)
        => CreateRuntimeOptions(
            appBaseDirectory,
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Path.GetTempPath(),
            Environment.CurrentDirectory);

    public static CliToolRuntimeOptions CreateRuntimeOptions(
        string appBaseDirectory,
        string? localAppData,
        string tempPath,
        string currentDirectory)
    {
        var resolution = Resolve(localAppData, tempPath, currentDirectory);
        return new CliToolRuntimeOptions
        {
            RootDirectory = resolution.RootDirectory,
            BundledToolsDirectory = Path.Combine(appBaseDirectory, "bundled-tools"),
            LegacyRootDirectories = resolution.LegacyRootDirectories.ToList()
        };
    }

    public static DataHubCliToolStorageResolution Resolve(
        string? localAppData,
        string tempPath,
        string currentDirectory)
    {
        var hostRoots = GetHostRoots(localAppData, tempPath, currentDirectory).ToList();
        var legacyRootDirectories = FindLegacyRootDirectories(hostRoots).ToList();

        foreach (var hostRoot in hostRoots.Take(2))
        {
            var stableRoot = Path.Combine(hostRoot, StableHostId);
            if (CanUseDirectory(stableRoot))
            {
                return new DataHubCliToolStorageResolution(stableRoot, legacyRootDirectories);
            }
        }

        return new DataHubCliToolStorageResolution(
            Path.Combine(hostRoots.Last(), StableHostId),
            legacyRootDirectories);
    }

    private static IEnumerable<string> GetHostRoots(string? localAppData, string tempPath, string currentDirectory)
    {
        var preferredRoot = string.IsNullOrWhiteSpace(localAppData)
            ? currentDirectory
            : localAppData;

        yield return Path.Combine(preferredRoot, "Reimaginate", "DataHub", "CLI", "tools", "hosts");
        yield return Path.Combine(tempPath, "Reimaginate", "DataHub", "CLI", "tools", "hosts");
        yield return Path.Combine(currentDirectory, ".datahub-cli", "tools", "hosts");
    }

    private static IEnumerable<string> FindLegacyRootDirectories(IEnumerable<string> hostRoots)
    {
        return hostRoots
            .SelectMany(EnumerateLegacyHostDirectories)
            .GroupBy(candidate => Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate.Path)), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(candidate => candidate.ManifestLastWriteTimeUtc).First())
            .OrderByDescending(candidate => candidate.ManifestLastWriteTimeUtc)
            .ThenBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
            .Select(candidate => candidate.Path);
    }

    private static IEnumerable<LegacyHostDirectory> EnumerateLegacyHostDirectories(string hostRoot)
    {
        if (!Directory.Exists(hostRoot))
        {
            yield break;
        }

        IEnumerable<string> directories;
        try
        {
            directories = Directory.EnumerateDirectories(hostRoot).ToList();
        }
        catch (UnauthorizedAccessException)
        {
            yield break;
        }
        catch (IOException)
        {
            yield break;
        }

        foreach (var directory in directories)
        {
            if (string.Equals(Path.GetFileName(directory), StableHostId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var manifestPath = Path.Combine(directory, "tools.json");
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            DateTime manifestLastWriteTimeUtc;
            try
            {
                manifestLastWriteTimeUtc = File.GetLastWriteTimeUtc(manifestPath);
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (IOException)
            {
                continue;
            }

            yield return new LegacyHostDirectory(directory, manifestLastWriteTimeUtc);
        }
    }

    private static bool CanUseDirectory(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            VerifyDirectoryReadWriteAccess(path);
            VerifyExistingPackageCacheAccess(path);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static void VerifyDirectoryReadWriteAccess(string path)
    {
        var probePath = Path.Combine(path, $".datahub-cli-write-test-{Guid.NewGuid():N}");
        File.WriteAllText(probePath, string.Empty);
        File.Delete(probePath);
    }

    private static void VerifyExistingPackageCacheAccess(string path)
    {
        var packageRoot = Path.Combine(path, "packages");
        if (!Directory.Exists(packageRoot))
        {
            return;
        }

        foreach (var packageDirectory in Directory.EnumerateDirectories(packageRoot))
        {
            foreach (var versionDirectory in Directory.EnumerateDirectories(packageDirectory))
            {
                _ = Directory.EnumerateFiles(versionDirectory, "*.nuspec").Take(1).ToList();
            }
        }
    }

    private sealed record LegacyHostDirectory(string Path, DateTime ManifestLastWriteTimeUtc);
}

public sealed record DataHubCliToolStorageResolution(
    string RootDirectory,
    IReadOnlyList<string> LegacyRootDirectories);