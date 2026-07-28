namespace Reimaginate.DataHub.SharedModels.Core;

public class AutoNumberSequence : CosmosDocument
{
    public AutoNumberSequence()
    {
        _dt = nameof(AutoNumberSequence);
    }

    public string SequenceName { get; set; }
    public string Prefix { get; set; }
    public string Suffix { get; set; }
    public int PaddingLength { get; set; }
    public long StartAt { get; set; } = 1;
    public long IncrementBy { get; set; } = 1;
    // Last reserved value. Leave as 0 for the common StartAt=1, IncrementBy=1 sequence.
    public long CurrentValue { get; set; }
}
