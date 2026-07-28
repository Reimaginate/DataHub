using System.Runtime.Serialization;

namespace Reimaginate.DataHub.VirtualEntities.Abstractions.QueryExpression;

[DataContract]
public class VirtualEntityQueryExpression
{
    public VirtualEntityQueryExpression()
    {
        Columns = new List<string>();
        Orders = new List<string>();
        Conditions = new List<LogicalCondition>();
    }

    [DataMember]
    public string DataHubEntityType { get; set; } = null!;

    [DataMember]
    public string DataSource { get; set; } = null!;

    [DataMember]
    public List<string> Columns { get; set; }

    [DataMember]
    public List<string> Orders { get; set; }

    [DataMember]
    public List<LogicalCondition> Conditions { get; set; }

    [DataMember]
    public string SourceEntityType { get; set; } = null!;

    [DataMember] public PageInfo PageInfo { get; set; } = new();
}
