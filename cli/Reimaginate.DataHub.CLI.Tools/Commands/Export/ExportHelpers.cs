using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.CLI.Tools.Shared.API;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Export;

public static class ExportHelpers
{
    public static (ExportFileType FileType, string FilePath) DetermineFileTypeAndPath(string? saveTo)
    {
        const string defaultFileName = "export.zip";
        var defaultFileType = ExportFileType.Zip;
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), defaultFileName);
        var fileType = defaultFileType;

        if (!string.IsNullOrWhiteSpace(saveTo))
        {
            var extension = Path.GetExtension(saveTo).ToLowerInvariant();
            fileType = extension switch
            {
                ".json" => ExportFileType.Json,
                ".zip" => ExportFileType.Zip,
                _ => ExportFileType.Folder
            };
            filePath = Path.GetFullPath(saveTo);
        }

        return (fileType, filePath);
    }

    public static void EnsureDirectoryExists(string filePath, bool isDirectory = false)
    {
        if (string.IsNullOrEmpty(filePath)) return;

        if (isDirectory)
        {
            if (!Directory.Exists(filePath))
            {
                Directory.CreateDirectory(filePath);
            }

            return;
        }

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public static async Task ExportToZipAsync(ICLIApi adminApi, string where, string zipPath, CancellationToken cancellationToken)
    {
        await using var zipStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create);

        await foreach (var response in FetchEntitiesWhereAsync(adminApi, where, cancellationToken))
        {
            if (response.Results?.Count > 0)
            {
                await SaveEntitiesToZipAsync(response.Results, archive);
            }

            if (!response.MoreResultsAvailable)
            {
                break;
            }
        }
    }

    public static async Task ExportToZipAsync(ICLIApi adminApi, string entityType, List<string> entityIds, string zipPath, CancellationToken cancellationToken)
    {
        await using var zipStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create);

        await foreach (var response in FetchEntitiesByIdAsync(adminApi, entityType, entityIds, cancellationToken))
        {
            if (response.Results?.Count > 0)
            {
                await SaveEntitiesToZipAsync(response.Results, archive);
            }

            if (!response.MoreResultsAvailable)
            {
                break;
            }
        }
    }

    public static async Task ExportToSingleJsonAsync(ICLIApi adminApi, string where, string jsonPath, CancellationToken cancellationToken)
    {
        await using var fileStream = new FileStream(jsonPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await using var streamWriter = new StreamWriter(fileStream);
        await using var jsonWriter = new JsonTextWriter(streamWriter);
        jsonWriter.Formatting = Formatting.Indented;

        await jsonWriter.WriteStartArrayAsync(cancellationToken);

        await foreach (var response in FetchEntitiesWhereAsync(adminApi, where, cancellationToken))
        {
            if (response.Results?.Count > 0)
            {
                foreach (var result in response.Results)
                {
                    await result.WriteToAsync(jsonWriter, cancellationToken);
                }
            }

            if (!response.MoreResultsAvailable)
            {
                break;
            }
        }

        await jsonWriter.WriteEndArrayAsync(cancellationToken);
    }

    public static async Task ExportToSingleJsonAsync(ICLIApi adminApi, string entityType, List<string> entityIds, string jsonPath, CancellationToken cancellationToken)
    {
        await using var fileStream = new FileStream(jsonPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await using var streamWriter = new StreamWriter(fileStream);
        await using var jsonWriter = new JsonTextWriter(streamWriter);
        jsonWriter.Formatting = Formatting.Indented;

        await jsonWriter.WriteStartArrayAsync(cancellationToken);

        await foreach (var response in FetchEntitiesByIdAsync(adminApi, entityType, entityIds, cancellationToken))
        {
            if (response.Results?.Count > 0)
            {
                foreach (var result in response.Results)
                {
                    await result.WriteToAsync(jsonWriter, cancellationToken);
                }
            }

            if (!response.MoreResultsAvailable)
            {
                break;
            }
        }

        await jsonWriter.WriteEndArrayAsync(cancellationToken);
    }

    public static async Task ExportToFolderAsync(ICLIApi adminApi, string where, string folderPath, CancellationToken cancellationToken)
    {
        await foreach (var response in FetchEntitiesWhereAsync(adminApi, where, cancellationToken))
        {
            if (response.Results?.Count > 0)
            {
                await SaveEntitiesToFolderAsync(response.Results, folderPath);
            }

            if (!response.MoreResultsAvailable)
            {
                break;
            }
        }
    }

    public static async Task ExportToFolderAsync(ICLIApi adminApi, string entityType, List<string> entityIds, string folderPath, CancellationToken cancellationToken)
    {
        await foreach (var response in FetchEntitiesByIdAsync(adminApi, entityType, entityIds, cancellationToken))
        {
            if (response.Results?.Count > 0)
            {
                await SaveEntitiesToFolderAsync(response.Results, folderPath);
            }

            if (!response.MoreResultsAvailable)
            {
                break;
            }
        }
    }


    public static async IAsyncEnumerable<GetEntitiesResponse> FetchEntitiesWhereAsync(ICLIApi adminApi, string where, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string? continuationToken = null;

        while (true)
        {
            var request = new GetEntitiesWhereRequest
            {
                WhereClause = where,
                PageSize = -1,
                ContinuationToken = continuationToken
            };

            var serializedRequest = new SerializedRequest
            {
                RequestType = nameof(GetEntitiesWhereRequest),
                Data = JsonConvert.SerializeObject(request)
            };

            var response = await adminApi.PostAdminMessage<GetEntitiesResponse>(serializedRequest, cancellationToken);
            yield return response;

            if (!response.MoreResultsAvailable)
            {
                break;
            }

            continuationToken = response.ContinuationToken;
        }
    }

    public static async IAsyncEnumerable<GetEntitiesResponse> FetchEntitiesByIdAsync(ICLIApi adminApi, string entityType, List<string> entityIds, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var entityIdsToProcess = new List<string>(entityIds);

        while (entityIdsToProcess.Any())
        {
            var batch = entityIdsToProcess.Take(500).ToList();

            var request = new GetEntitiesByIdRequest()
            {
                EntityType = entityType,
                EntityIds = batch
            };

            var serializedRequest = new SerializedRequest
            {
                RequestType = nameof(GetEntitiesByIdRequest),
                Data = JsonConvert.SerializeObject(request)
            };

            var response = await adminApi.PostAdminMessage<GetEntitiesResponse>(serializedRequest, cancellationToken);
            yield return response;

            entityIdsToProcess.RemoveRange(0, batch.Count);
        }
    }

    public static async Task SaveEntitiesToZipAsync(List<JObject> results, ZipArchive archive)
    {
        foreach (var result in results)
        {
            var fileName = GetEntityFileName(result);
            var jsonEntry = archive.CreateEntry(fileName, CompressionLevel.Optimal);

            await using var entryStream = jsonEntry.Open();
            await using var streamWriter = new StreamWriter(entryStream);
            await streamWriter.WriteAsync(result.ToString(Formatting.Indented));
        }
    }

    public static async Task SaveEntitiesToFolderAsync(List<JObject> results, string folderPath)
    {
        foreach (var result in results)
        {
            var fileName = GetEntityFileName(result);
            var filePath = Path.Combine(folderPath, fileName);
            await File.WriteAllTextAsync(filePath, result.ToString(Formatting.Indented));
        }
    }

    public static string GetEntityFileName(JObject entity)
    {
        var entityType = entity.Value<string>(nameof(DataHubEntity.entityType)) ?? "UnknownType";
        var entityId = entity.Value<string>(nameof(DataHubEntity.id)) ?? Guid.NewGuid().ToString();
        return $"{SanitizeFileName(entityType)}_{SanitizeFileName(entityId)}.json";
    }

    public static string SanitizeFileName(string name)
    {
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalidChar, '_');
        }
        return name;
    }

    public enum ExportFileType
    {
        Zip,
        Json,
        Folder
    }
}
