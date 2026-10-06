using System.Runtime.Serialization;

namespace Reimaginate.DataHub.VirtualTables.Abstractions.QueryExpression;

[DataContract]
public class OrCondition : LogicalCondition
{
    public OrCondition()
    {
        LogicalOperator = "or";
    }
}