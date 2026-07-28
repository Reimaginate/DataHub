using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocument;

public class GetCosmosDocumentResponse<T> where T : CosmosDocument
{ 
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public T Result { get; set; }
}