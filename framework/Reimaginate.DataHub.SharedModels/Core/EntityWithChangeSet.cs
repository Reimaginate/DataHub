using System;
using Newtonsoft.Json.Linq;

namespace Reimaginate.DataHub.SharedModels.Core;

public class EntityWithChangeSet
{
    public JObject Entity { get; set; }
    public JObject ChangeSet { get; set; }
    public DateTimeOffset SourceEventTimeStamp { get; set; }
}