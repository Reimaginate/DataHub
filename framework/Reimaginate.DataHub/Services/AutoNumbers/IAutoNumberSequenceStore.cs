using System.Threading;
using System.Threading.Tasks;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.Services.AutoNumbers;

internal interface IAutoNumberSequenceStore
{
    Task<AutoNumberSequence> GetSequenceAsync(string sequenceName, CancellationToken cancellationToken);
    Task<AutoNumberSequence> TryIncrementSequenceAsync(AutoNumberSequence sequence, long incrementAmount, string etag, CancellationToken cancellationToken);
}
