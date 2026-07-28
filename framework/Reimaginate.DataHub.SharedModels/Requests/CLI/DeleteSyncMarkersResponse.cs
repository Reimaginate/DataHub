using System;
using System.Collections.Generic;

namespace Reimaginate.DataHub.SharedModels.Requests.CLI;

public class DeleteSyncMarkersResponse
{
    public bool Success { get; set; }
    public string FailureReason { get; set; }
    public List<Exception> Exceptions { get; set; }
}