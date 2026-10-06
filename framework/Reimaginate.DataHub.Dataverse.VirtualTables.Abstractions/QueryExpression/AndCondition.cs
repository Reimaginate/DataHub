using System.Runtime.Serialization;

namespace Reimaginate.DataHub.Dataverse.VirtualTables.Abstractions.QueryExpression;

[DataContract]
public class AndCondition : LogicalCondition
{
    public AndCondition()
    {
        LogicalOperator = "and";
    }
}