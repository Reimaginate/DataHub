using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.DataAccess.Commands.DeleteCosmosDocuments;

public class DeleteCosmosDocumentsCommand<T> : IRequest<DeleteCosmosDocumentsResponse<T>> where T : CosmosDocument
{

    public DeleteCosmosDocumentsCommand()
    { }

    public DeleteCosmosDocumentsCommand(List<T> documents)
    {
        Documents = documents;
    }

    public List<T> Documents { get; set; } = new();
}