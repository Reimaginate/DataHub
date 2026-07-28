using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Reimaginate.DataHub.SharedModels.Core.Models.Duplicates;

public class DuplicateMergePlan
{
    public bool AutoMerge { get; set; }
    public string EntityType { get; set; }
    public List<string> EntityIds { get; set; }
    public bool IgnoreArchivedEntities { get; set; } = true;
    public string SurvivingEntityId { get; set; }
    public JToken SurvivingEntityData { get; set; }
    public JToken NonSurvivingEntityData { get; set; }
}