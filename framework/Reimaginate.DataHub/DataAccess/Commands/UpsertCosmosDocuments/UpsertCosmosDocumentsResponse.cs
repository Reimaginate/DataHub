using System.Collections.Generic;
using Reimaginate.DataHub.Models;

namespace Reimaginate.DataHub.DataAccess.Commands.UpsertCosmosDocuments;

public class UpsertCosmosDocumentsResponse<T>
{
    public List<T> Successes { get; set; }
    public List<DataAccessFailure<T>> Failures { get; set; }
}