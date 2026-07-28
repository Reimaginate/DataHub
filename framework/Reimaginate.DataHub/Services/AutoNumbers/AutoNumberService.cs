using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.Client;

namespace Reimaginate.DataHub.Services.AutoNumbers;

internal class AutoNumberService(IAutoNumberSequenceStore sequenceStore) : IAutoNumberService
{
    private const int MaxConcurrencyRetries = 25;

    public async Task<List<ReservedAutoNumber>> ReserveAsync(string sequenceName, int count, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxConcurrencyRetries; attempt++)
        {
            var sequence = await sequenceStore.GetSequenceAsync(sequenceName, cancellationToken);
            Validate(sequence);

            var incrementAmount = GetIncrementAmount(sequence, count);
            var updatedSequence = await sequenceStore.TryIncrementSequenceAsync(sequence, incrementAmount, sequence._etag, cancellationToken);
            if (updatedSequence != null)
            {
                return CreateReservedNumbers(updatedSequence, count);
            }
        }

        throw new AutoNumberReservationException($"Could not reserve auto numbers for sequence '{sequenceName}' because the sequence was modified too many times.");
    }

    private static List<ReservedAutoNumber> CreateReservedNumbers(AutoNumberSequence sequence, int count)
    {
        var firstValue = sequence.CurrentValue - ((count - 1) * sequence.IncrementBy);
        var reservedNumbers = new List<ReservedAutoNumber>(count);

        for (var i = 0; i < count; i++)
        {
            var sequenceValue = firstValue + (i * sequence.IncrementBy);
            reservedNumbers.Add(new ReservedAutoNumber
            {
                SequenceValue = sequenceValue,
                Value = AutoNumberFormatter.Format(sequence, sequenceValue)
            });
        }

        return reservedNumbers;
    }

    private static long GetIncrementAmount(AutoNumberSequence sequence, int count)
    {
        var firstValue = sequence.CurrentValue == default && sequence.StartAt != default
            ? sequence.StartAt
            : sequence.CurrentValue + sequence.IncrementBy;

        var lastValue = firstValue + ((count - 1) * sequence.IncrementBy);
        return lastValue - sequence.CurrentValue;
    }

    private static void Validate(AutoNumberSequence sequence)
    {
        if (sequence.IncrementBy == 0)
        {
            throw new AutoNumberReservationException($"Auto number sequence '{sequence.SequenceName}' has an invalid IncrementBy value of 0.");
        }

        if (sequence.PaddingLength < 0)
        {
            throw new AutoNumberReservationException($"Auto number sequence '{sequence.SequenceName}' has an invalid PaddingLength value of {sequence.PaddingLength}.");
        }
    }
}
