using System;
using System.Collections.Generic;

namespace Reimaginate.DataHub.Requests.Internal.DetachEntitiesFromDataSource;

public class DetachEntitiesFromDataSourceResponse
{
    public List<Exception> Failures { get; set; }
}