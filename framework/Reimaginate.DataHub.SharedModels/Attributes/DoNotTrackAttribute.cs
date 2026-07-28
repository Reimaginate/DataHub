using System;

namespace Reimaginate.DataHub.SharedModels.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class DoNotTrackAttribute : Attribute
{
    public bool DoNotTrack { get; }
    public string Sources { get; set; }

    public DoNotTrackAttribute()
    {
        DoNotTrack = true;
    }

    public DoNotTrackAttribute(string sources)
    {
        DoNotTrack = true;
        Sources = sources;
    }

    public DoNotTrackAttribute(bool doNotTrack)
    {
        DoNotTrack = doNotTrack;
    }

    public DoNotTrackAttribute(bool doNotTrack, string sources)
    {
        DoNotTrack = doNotTrack;
        Sources = sources;
    }
}