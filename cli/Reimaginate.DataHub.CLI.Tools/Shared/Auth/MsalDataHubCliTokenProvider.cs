using Microsoft.Identity.Client;
using Reimaginate.DataHub.CLI.Tools.Shared.Models;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Reimaginate.DataHub.CLI.Tools.Shared.Auth;

public interface IDataHubMsalClient
{
    Task<string?> AcquireTokenSilentAsync(string[] scopes, CancellationToken cancellationToken);
    Task<string> AcquireTokenInteractiveAsync(string[] scopes, CancellationToken cancellationToken);
    Task<bool> HasCachedTokenAsync(string[] scopes, CancellationToken cancellationToken);
    Task ClearTokenCacheAsync(CancellationToken cancellationToken);
}

public sealed class MsalDataHubCliTokenProvider(
    Func<DataHubConnection, IDataHubMsalClient> clientFactory,
    TimeSpan? interactiveTokenTimeout = null) : IDataHubCliTokenProvider
{
    private static readonly TimeSpan SilentTokenTimeout = TimeSpan.FromSeconds(15);

    public MsalDataHubCliTokenProvider()
        : this(connection => new DataHubMsalClient(connection))
    {
    }

    public async Task<string> GetAccessTokenAsync(DataHubConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ValidateScope(connection);

        var scopes = new[] { connection.Scope };
        var client = clientFactory(connection);

        try
        {
            var silentToken = await AcquireTokenSilentWithTimeoutAsync(client, scopes, cancellationToken);
            if (!string.IsNullOrWhiteSpace(silentToken))
            {
                return silentToken;
            }

            try
            {
                return await AcquireTokenInteractiveWithTimeoutAsync(client, scopes, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                MsalAuthDiagnostics.Write($"interactive token acquisition failed: {ex.GetType().Name}: {ex.Message}");
                var recoveredToken = await TryAcquireTokenSilentAfterInteractiveTimeoutAsync(client, scopes, ex, cancellationToken);
                if (!string.IsNullOrWhiteSpace(recoveredToken))
                {
                    return recoveredToken;
                }

                throw;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw CreateActionableException(connection, ex);
        }
    }

    public async Task<string> GetAccessTokenSilentAsync(DataHubConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ValidateScope(connection);

        var scopes = new[] { connection.Scope };
        var client = clientFactory(connection);

        try
        {
            var silentToken = await AcquireTokenSilentWithTimeoutAsync(client, scopes, cancellationToken);
            if (!string.IsNullOrWhiteSpace(silentToken))
            {
                return silentToken;
            }

            throw new InvalidOperationException(
                $"No cached MSAL token is available for DataHub target '{connection.Name}'. Run 'datahub login --profile {connection.Name}'.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw CreateActionableException(connection, ex);
        }
    }

    public async Task<string> LoginAsync(DataHubConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ValidateScope(connection);

        var scopes = new[] { connection.Scope };
        var client = clientFactory(connection);

        try
        {
            string token;
            try
            {
                MsalAuthDiagnostics.Write("login interactive token acquisition started.");
                token = await AcquireTokenInteractiveWithTimeoutAsync(client, scopes, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                MsalAuthDiagnostics.Write($"login interactive token acquisition failed: {ex.GetType().Name}: {ex.Message}");
                var silentToken = await TryAcquireTokenSilentAfterInteractiveTimeoutAsync(client, scopes, ex, cancellationToken);
                if (!string.IsNullOrWhiteSpace(silentToken))
                {
                    return silentToken;
                }

                throw;
            }

            await EnsureInteractiveLoginPersistedAsync(connection, scopes, cancellationToken);
            return token;
        }
        catch (DataHubMsalCachePersistenceException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw CreateActionableException(connection, ex);
        }
    }

    private async Task EnsureInteractiveLoginPersistedAsync(
        DataHubConnection connection,
        string[] scopes,
        CancellationToken cancellationToken)
    {
        const int attempts = 5;
        var delay = TimeSpan.FromMilliseconds(200);

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var verificationClient = clientFactory(connection);
            if (await verificationClient.HasCachedTokenAsync(scopes, cancellationToken))
            {
                MsalAuthDiagnostics.Write($"login cache verification succeeded on attempt {attempt}.");
                return;
            }

            MsalAuthDiagnostics.Write($"login cache verification did not find an account on attempt {attempt}.");
            if (attempt < attempts)
            {
                await Task.Delay(delay, cancellationToken);
            }
        }

        throw new DataHubMsalCachePersistenceException(
            $"MSAL interactive sign-in completed for DataHub target '{connection.Name}', but no usable token was persisted to the MSAL cache for scope '{connection.Scope}'. " +
            "Run 'datahub login' again with DATAHUB_AUTH_DEBUG=1 and check the reported cache path.");
    }

    public async Task LogoutAsync(DataHubConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        try
        {
            await clientFactory(connection).ClearTokenCacheAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException(
                $"MSAL logout failed for DataHub target '{connection.Name}'. MSAL reported: {ex.Message}",
                ex);
        }
    }

    private static async Task<string?> TryAcquireTokenSilentAfterInteractiveTimeoutAsync(
        IDataHubMsalClient client,
        string[] scopes,
        Exception interactiveException,
        CancellationToken cancellationToken)
    {
        if (interactiveException is not TimeoutException)
        {
            return null;
        }

        Console.Error.WriteLine($"{interactiveException.Message} Checking the MSAL cache before reporting interactive sign-in failure.");

        try
        {
            return await AcquireTokenSilentWithTimeoutAsync(client, scopes, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.Error.WriteLine($"MSAL cache check after browser sign-in did not return a token: {ex.Message}");
            return null;
        }
    }

    private static async Task<string?> AcquireTokenSilentWithTimeoutAsync(
        IDataHubMsalClient client,
        string[] scopes,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(SilentTokenTimeout);

        try
        {
            return await client.AcquireTokenSilentAsync(scopes, timeout.Token).WaitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"MSAL did not return a cached token within {SilentTokenTimeout.TotalSeconds:0} seconds.");
        }
    }

    private async Task<string> AcquireTokenInteractiveWithTimeoutAsync(
        IDataHubMsalClient client,
        string[] scopes,
        CancellationToken cancellationToken)
    {
        if (interactiveTokenTimeout is not { } timeoutDuration || timeoutDuration <= TimeSpan.Zero)
        {
            return await client.AcquireTokenInteractiveAsync(scopes, cancellationToken);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(timeoutDuration);

        try
        {
            return await client.AcquireTokenInteractiveAsync(scopes, timeout.Token).WaitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"MSAL interactive token acquisition did not complete within {timeoutDuration.TotalSeconds:0} seconds.");
        }
    }

    private static void ValidateScope(DataHubConnection connection)
    {
        if (string.IsNullOrWhiteSpace(connection.Scope))
        {
            throw new InvalidOperationException($"Connection '{connection.Name}' does not define a DataHub API scope.");
        }
    }

    private static InvalidOperationException CreateActionableException(DataHubConnection connection, Exception innerException)
    {
        var tenantMessage = string.IsNullOrWhiteSpace(connection.TenantId)
            ? "the configured tenant"
            : $"tenant '{connection.TenantId}'";

        return new InvalidOperationException(
            $"MSAL authentication failed for DataHub target '{connection.Name}'. " +
            $"Complete interactive sign-in for scope '{connection.Scope}' in {tenantMessage}. " +
            $"MSAL reported: {innerException.Message}",
            innerException);
    }
}

internal sealed class DataHubMsalCachePersistenceException(string message) : InvalidOperationException(message);

internal static class MsalAuthDiagnostics
{
    public static bool IsEnabled
        => IsTruthy(Environment.GetEnvironmentVariable("DATAHUB_AUTH_DEBUG"));

    public static void Write(string message)
    {
        if (!IsEnabled)
        {
            return;
        }

        Console.Error.WriteLine($"[datahub-auth] {DateTimeOffset.Now:O} {message}");
    }

    public static void WriteMsal(LogLevel logLevel, string message, bool containsPii)
    {
        if (!IsEnabled || containsPii)
        {
            return;
        }

        Console.Error.WriteLine($"[datahub-auth][msal:{logLevel}] {message}");
    }

    private static bool IsTruthy(string? value)
        => string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
           || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
           || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
}

public sealed class DataHubMsalClient : IDataHubMsalClient
{
    private const string TokenCacheDirectoryName = "auth";
    private const string TokenCacheFileName = "msal.cache";
    private static readonly SemaphoreSlim TokenCacheLock = new(1, 1);

    private readonly IPublicClientApplication _application;

    public string ClientId { get; }

    public DataHubMsalClient(DataHubConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        ClientId = DataHubCliAuthenticationDefaults.ClientId;
        var builder = PublicClientApplicationBuilder
            .Create(ClientId)
            .WithDefaultRedirectUri()
            .WithLegacyCacheCompatibility(false);

        if (!string.IsNullOrWhiteSpace(connection.TenantId))
        {
            builder.WithAuthority(AzureCloudInstance.AzurePublic, connection.TenantId);
        }

        if (MsalAuthDiagnostics.IsEnabled)
        {
            builder.WithLogging(MsalAuthDiagnostics.WriteMsal, LogLevel.Verbose, false, false);
        }

        _application = builder.Build();
        ConfigureTokenCache(_application.UserTokenCache);
    }

    public async Task<string?> AcquireTokenSilentAsync(string[] scopes, CancellationToken cancellationToken)
    {
        MsalAuthDiagnostics.Write("silent token acquisition: reading accounts.");
        var accounts = (await _application.GetAccountsAsync()).ToArray();
        if (accounts.Length == 0)
        {
            MsalAuthDiagnostics.Write("silent token acquisition: no account in cache.");
            return null;
        }

        MsalAuthDiagnostics.Write($"silent token acquisition: found {accounts.Length} cached account(s).");
        foreach (var account in accounts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                MsalAuthDiagnostics.Write("silent token acquisition: executing MSAL silent request.");
                var result = await _application
                    .AcquireTokenSilent(scopes, account)
                    .ExecuteAsync(cancellationToken);

                MsalAuthDiagnostics.Write("silent token acquisition: token returned.");
                return result.AccessToken;
            }
            catch (MsalUiRequiredException ex)
            {
                MsalAuthDiagnostics.Write($"silent token acquisition: cached account cannot satisfy request: {ex.ErrorCode}.");
            }
        }

        MsalAuthDiagnostics.Write("silent token acquisition: no cached account could satisfy the requested scopes.");
        return null;
    }

    public async Task<string> AcquireTokenInteractiveAsync(string[] scopes, CancellationToken cancellationToken)
    {
        MsalAuthDiagnostics.Write("interactive token acquisition: executing MSAL interactive request.");
        var result = await _application
            .AcquireTokenInteractive(scopes)
            .ExecuteAsync(cancellationToken);

        MsalAuthDiagnostics.Write("interactive token acquisition: token returned.");
        return result.AccessToken;
    }

    public async Task<bool> HasCachedTokenAsync(string[] scopes, CancellationToken cancellationToken)
    {
        var token = await AcquireTokenSilentAsync(scopes, cancellationToken);
        var hasToken = !string.IsNullOrWhiteSpace(token);
        MsalAuthDiagnostics.Write($"cache token check: {(hasToken ? "token found" : "no token found")}.");
        return hasToken;
    }

    public async Task ClearTokenCacheAsync(CancellationToken cancellationToken)
    {
        var accounts = await _application.GetAccountsAsync();
        foreach (var account in accounts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _application.RemoveAsync(account);
        }
    }

    private static void ConfigureTokenCache(ITokenCache tokenCache)
    {
        tokenCache.SetBeforeAccessAsync(async args =>
        {
            await TokenCacheLock.WaitAsync();
            try
            {
                var cachePath = GetTokenCachePath();
                MsalAuthDiagnostics.Write($"MSAL token cache path: {cachePath}");
                if (!File.Exists(cachePath))
                {
                    MsalAuthDiagnostics.Write("MSAL token cache read skipped: file does not exist.");
                    return;
                }

                var protectedCacheBytes = await File.ReadAllBytesAsync(cachePath);
                if (protectedCacheBytes.Length == 0)
                {
                    MsalAuthDiagnostics.Write("MSAL token cache read skipped: file is empty.");
                    return;
                }

                try
                {
                    var cacheBytes = UnprotectCacheBytes(protectedCacheBytes);
                    args.TokenCache.DeserializeMsalV3(cacheBytes, shouldClearExistingCache: true);
                    MsalAuthDiagnostics.Write(
                        $"MSAL token cache read {protectedCacheBytes.Length} byte(s), deserialized {cacheBytes.Length} byte(s).");
                }
                catch (Exception ex) when (ex is CryptographicException or InvalidDataException or MsalClientException)
                {
                    MsalAuthDiagnostics.Write(
                        $"MSAL token cache read ignored because the existing cache could not be deserialized: {ex.GetType().Name}: {ex.Message}");
                }
            }
            finally
            {
                TokenCacheLock.Release();
            }
        });

        tokenCache.SetAfterAccessAsync(async args =>
        {
            if (!args.HasStateChanged)
            {
                MsalAuthDiagnostics.Write("MSAL token cache write skipped: cache state did not change.");
                return;
            }

            await TokenCacheLock.WaitAsync();
            try
            {
                var cachePath = GetTokenCachePath();
                var cacheDirectoryPath = GetTokenCacheDirectoryPath();
                Directory.CreateDirectory(cacheDirectoryPath);

                var cacheBytes = args.TokenCache.SerializeMsalV3();
                var protectedCacheBytes = ProtectCacheBytes(cacheBytes);
                await WriteAllBytesAtomicallyAsync(cachePath, protectedCacheBytes);
                MsalAuthDiagnostics.Write(
                    $"MSAL token cache wrote {protectedCacheBytes.Length} byte(s), serialized {cacheBytes.Length} byte(s).");
            }
            finally
            {
                TokenCacheLock.Release();
            }
        });
    }

    private static string GetTokenCacheDirectoryPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            localAppData = Path.GetTempPath();
        }

        return Path.Combine(localAppData, "Reimaginate", "DataHub", "CLI", TokenCacheDirectoryName);
    }

    private static string GetTokenCachePath()
        => Path.Combine(GetTokenCacheDirectoryPath(), TokenCacheFileName);

    private static byte[] ProtectCacheBytes(byte[] cacheBytes)
        => OperatingSystem.IsWindows()
            ? WindowsCurrentUserDataProtection.Protect(cacheBytes)
            : cacheBytes;

    private static byte[] UnprotectCacheBytes(byte[] protectedCacheBytes)
        => OperatingSystem.IsWindows()
            ? WindowsCurrentUserDataProtection.Unprotect(protectedCacheBytes)
            : protectedCacheBytes;

    private static async Task WriteAllBytesAtomicallyAsync(string path, byte[] bytes)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllBytesAsync(temporaryPath, bytes);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}

internal static class WindowsCurrentUserDataProtection
{
    private const int CryptProtectUiForbidden = 0x1;

    public static byte[] Protect(byte[] bytes)
        => Transform(bytes, protect: true);

    public static byte[] Unprotect(byte[] bytes)
        => Transform(bytes, protect: false);

    private static byte[] Transform(byte[] bytes, bool protect)
    {
        if (bytes.Length == 0)
        {
            return [];
        }

        var input = CreateBlob(bytes);
        var output = new DataBlob();

        try
        {
            var succeeded = protect
                ? CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, out output)
                : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, out output);

            if (!succeeded)
            {
                throw new CryptographicException(Marshal.GetLastWin32Error());
            }

            var result = new byte[output.DataLength];
            Marshal.Copy(output.Data, result, 0, output.DataLength);
            return result;
        }
        finally
        {
            if (input.Data != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(input.Data);
            }

            if (output.Data != IntPtr.Zero)
            {
                LocalFree(output.Data);
            }
        }
    }

    private static DataBlob CreateBlob(byte[] bytes)
    {
        var blob = new DataBlob
        {
            DataLength = bytes.Length,
            Data = Marshal.AllocHGlobal(bytes.Length)
        };

        Marshal.Copy(bytes, 0, blob.Data, bytes.Length);
        return blob;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(
        ref DataBlob dataIn,
        string? dataDescription,
        IntPtr optionalEntropy,
        IntPtr reserved,
        IntPtr promptStruct,
        int flags,
        out DataBlob dataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectData(
        ref DataBlob dataIn,
        IntPtr dataDescription,
        IntPtr optionalEntropy,
        IntPtr reserved,
        IntPtr promptStruct,
        int flags,
        out DataBlob dataOut);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int DataLength;
        public IntPtr Data;
    }
}
