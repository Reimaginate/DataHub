using System;

namespace Reimaginate.DataHub.SharedModels.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class RelatedEntityTypeAttribute : Attribute
{
    public string DataSource { get; }
    public string TypeName { get; }
    public string[] MappedPropertiesIn { get; }
    public string[] MappedPropertiesOut { get; }

    public RelatedEntityTypeAttribute(string dataSource, string typeName)
    {
        DataSource = dataSource;
        TypeName = typeName;
    }

    public RelatedEntityTypeAttribute(string dataSource, string typeName, string[] mappedPropertiesIn, string[] mappedPropertiesOut)
    {
        DataSource = dataSource;
        TypeName = typeName;
        MappedPropertiesIn = mappedPropertiesIn;
        MappedPropertiesOut = mappedPropertiesOut;
    }
}