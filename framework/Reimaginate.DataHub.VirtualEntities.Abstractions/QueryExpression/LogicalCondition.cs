using System.Runtime.Serialization;

namespace Reimaginate.DataHub.VirtualEntities.Abstractions.QueryExpression;

[DataContract]
public class LogicalCondition : ConditionBase
{
    public LogicalCondition()
    {
        Conditions = new List<ConditionBase>();
    }

    [DataMember]
    public string LogicalOperator { get; set; } = null!;

    [DataMember]
    public List<ConditionBase> Conditions { get; set; }
}
