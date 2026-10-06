using System.Runtime.Serialization;

namespace Reimaginate.DataHub.VirtualTables.Abstractions.QueryExpression;

[DataContract]
public class AndCondition : LogicalCondition
{
    public AndCondition()
    {
        LogicalOperator = "and";
    }
}