using Newtonsoft.Json.Linq;

namespace Reimaginate.DataHub.SharedModels.Core.Interfaces;

public interface IPreMergeRuleResolver
{
    public bool Resolve(JObject incomingEntity, JObject existingEntity, ChangeTrackingEntry changeSet);
}