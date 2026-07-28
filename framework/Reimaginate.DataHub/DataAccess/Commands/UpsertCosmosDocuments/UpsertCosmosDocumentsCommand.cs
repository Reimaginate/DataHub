using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.DataAccess.Commands.UpsertCosmosDocuments;

public class UpsertCosmosDocumentsCommand<T> : IRequest<UpsertCosmosDocumentsResponse<T>> where T : CosmosDocument
{
    public List<T> Documents { get; set; }
}