using System.Runtime.Serialization;

namespace Reimaginate.DataHub.VirtualEntities.Abstractions.QueryExpression;

[DataContract]
public class XOrCondition : ConditionBase
{
    [DataMember]
    public string Condition { get; set; } = "xor";

    [DataMember]
    public List<ConditionBase> Conditions { get; set; } = new();
}
