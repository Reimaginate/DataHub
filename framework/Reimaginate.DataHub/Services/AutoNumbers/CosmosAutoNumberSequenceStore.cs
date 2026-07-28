using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataServices.Cosmos;

namespace Reimaginate.DataHub.Services.AutoNumbers;

internal class CosmosAutoNumberSequenceStore(CosmosDataServiceOptions<AutoNumberSequence> options) : IAutoNumberSequenceStore
{
    private readonly Container _container = options.CosmosClient.GetContainer(options.DatabaseName, options.ContainerName);

    public async Task<AutoNumberSequence> GetSequenceAsync(string sequenceName, CancellationToken cancellationToken)
    {
        var sequence = await TryReadSequenceByIdAsync(sequenceName, cancellationToken) ??
                       await TryQuerySequenceByNameAsync(sequenceName, cancellationToken);

        if (sequence == null)
        {
            throw new AutoNumberSequenceNotFoundException(sequenceName);
        }

        EnsureDocumentIdentity(sequence, sequenceName);
        return sequence;
    }

    public async Task<AutoNumberSequence> TryIncrementSequenceAsync(AutoNumberSequence sequence, long incrementAmount, string etag, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(etag))
        {
            throw new AutoNumberReservationException($"Auto number sequence '{sequence.SequenceName}' did not include an ETag.");
        }

        EnsureDocumentIdentity(sequence, sequence.SequenceName);

        try
        {
            var response = await _container.PatchItemAsync<AutoNumberSequence>(
                sequence.id,
                new PartitionKey(GetPartitionKey(sequence)),
                [PatchOperation.Increment($"/{nameof(AutoNumberSequence.CurrentValue)}", incrementAmount)],
                new PatchItemRequestOptions { IfMatchEtag = etag },
                cancellationToken);

            response.Resource._etag = response.ETag;
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            return null;
        }
    }

    private async Task<AutoNumberSequence> TryReadSequenceByIdAsync(string sequenceName, CancellationToken cancellationToken)
    {
        var probe = new AutoNumberSequence
        {
            id = sequenceName,
            SequenceName = sequenceName
        };
        EnsureDocumentIdentity(probe, sequenceName);

        try
        {
            var response = await _container.ReadItemAsync<AutoNumberSequence>(
                sequenceName,
                new PartitionKey(GetPartitionKey(probe)),
                cancellationToken: cancellationToken);

            response.Resource._etag = response.ETag;
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private async Task<AutoNumberSequence> TryQuerySequenceByNameAsync(string sequenceName, CancellationToken cancellationToken)
    {
        var query = new QueryDefinition("select * from x where x._dt = @documentType and x.SequenceName = @sequenceName")
            .WithParameter("@documentType", nameof(AutoNumberSequence))
            .WithParameter("@sequenceName", sequenceName);

        using var iterator = _container.GetItemQueryIterator<AutoNumberSequence>(
            query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 2 });

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            var matches = page.Resource.ToList();
            if (matches.Count == 0)
            {
                continue;
            }

            if (matches.Count > 1)
            {
                throw new AutoNumberReservationException($"Multiple auto number sequences were found for '{sequenceName}'.");
            }

            return matches[0];
        }

        return null;
    }

    private void EnsureDocumentIdentity(AutoNumberSequence sequence, string sequenceName)
    {
        sequence.SequenceName ??= sequenceName;
        sequence.id ??= sequence.SequenceName;
        sequence._dt = nameof(AutoNumberSequence);
        sequence.Prefix ??= string.Empty;
        sequence.Suffix ??= string.Empty;
        options.SetItemPartitionKeyFunc?.Invoke(sequence);
    }

    private string GetPartitionKey(AutoNumberSequence sequence)
    {
        return options.GetItemPartitionKeyFunc?.Invoke(sequence) ?? string.Empty;
    }
}
