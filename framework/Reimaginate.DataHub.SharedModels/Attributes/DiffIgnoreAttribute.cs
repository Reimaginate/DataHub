using System;

namespace Reimaginate.DataHub.SharedModels.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class DiffIgnoreAttribute : Attribute
{
    public DiffIgnoreAttribute()
    {
    }
}