using System.Collections.Generic;
using Reimaginate.Mapper;

namespace Reimaginate.DataHub.SharedModels.Core.Interfaces;

public interface IDataHubTypeMapper<T1, T2> : ITypeMapper<T1, T2>
{
    public List<string> MappedEntityReferences { get; set; }
    public List<string> MappedProperties { get; set; }
}