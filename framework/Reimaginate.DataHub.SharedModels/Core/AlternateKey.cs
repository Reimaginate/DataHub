using System;

namespace Reimaginate.DataHub.SharedModels.Core;

public class AlternateKey
{
    public AlternateKey() { }

    public AlternateKey(string key, string value)
    {
        Key = key;
        Value = value;
    }

    public string Key { get; set; }
    public string Value { get; set; }
    public DateTimeOffset? LastSync { get; set; }
    public DateTimeOffset? LastMerge { get; set; }
    public string SyncError { get; set; }
    public string MergeError { get; set; }
}