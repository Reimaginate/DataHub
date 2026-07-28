using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.DataAccess.Commands.CreateCosmosDocuments;

public class CreateCosmosDocumentsCommand<T> : IRequest<CreateCosmosDocumentsResponse<T>> where T : CosmosDocument
{
    public List<T> Documents { get; set; }
}