using System.Runtime.Serialization;

namespace Reimaginate.DataHub.Dataverse.VirtualTables.Abstractions.QueryExpression;

[DataContract]
public class OrCondition : LogicalCondition
{
    public OrCondition()
    {
        LogicalOperator = "or";
    }
}