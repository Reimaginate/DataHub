using System.Runtime.Serialization;

namespace Reimaginate.DataHub.VirtualEntities.Abstractions.QueryExpression;

public class PageInfo
{
    [DataMember] public int PageNo { get; set; } = 1;
    [DataMember] public int PageSize { get; set; } = 50;
}