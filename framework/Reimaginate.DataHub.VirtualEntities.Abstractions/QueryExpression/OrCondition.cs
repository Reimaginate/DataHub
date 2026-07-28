using System.Runtime.Serialization;

namespace Reimaginate.DataHub.VirtualEntities.Abstractions.QueryExpression;

[DataContract]
public class OrCondition : LogicalCondition
{
    public OrCondition()
    {
        LogicalOperator = "or";
    }
}