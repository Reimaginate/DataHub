using System.Collections.Generic;
using Reimaginate.DataHub.Models;

namespace Reimaginate.DataHub.DataAccess.Commands.DeleteCosmosDocuments;

public class DeleteCosmosDocumentsResponse<T>
{
    public List<T> Successes { get; set; }

    public List<DataAccessFailure<T>> Failures { get; set; }
}