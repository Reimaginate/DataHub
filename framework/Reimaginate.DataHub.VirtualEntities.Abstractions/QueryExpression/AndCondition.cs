using System.Runtime.Serialization;

namespace Reimaginate.DataHub.VirtualEntities.Abstractions.QueryExpression;

[DataContract]
public class AndCondition : LogicalCondition
{
    public AndCondition()
    {
        LogicalOperator = "and";
    }
}