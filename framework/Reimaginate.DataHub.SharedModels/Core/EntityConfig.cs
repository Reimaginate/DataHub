using System.Collections.Generic;
using Reimaginate.DataHub.SharedModels.Rules;

namespace Reimaginate.DataHub.SharedModels.Core;

public class EntityConfig : CosmosDocument
{
    public EntityConfig()
    {
        _dt = nameof(EntityConfig);
    }

    public string EntityType { get; set; }
    public List<DuplicatePreventionRule> DuplicatePreventionRules { get; set; } = new();
    public List<MergeRule> MergeRules { get; set; } = new();
    public List<PreMergeRule> PreMergeRules { get; set; } = new();
}