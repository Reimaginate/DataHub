using System.Runtime.Serialization;

namespace Reimaginate.DataHub.Dataverse.VirtualTables.Abstractions.QueryExpression;

[DataContract]
public class Condition : ConditionBase
{
    [DataMember]
    public string PropertyName { get; set; } = null!;

    [DataMember]
    public string Operator { get; set; } = null!;

    [DataMember]
    public object Value { get; set; } = null!;
}
