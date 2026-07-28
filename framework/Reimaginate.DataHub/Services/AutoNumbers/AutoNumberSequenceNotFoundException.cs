namespace Reimaginate.DataHub.Services.AutoNumbers;

public class AutoNumberSequenceNotFoundException(string sequenceName) : AutoNumberReservationException($"Auto number sequence '{sequenceName}' was not found.")
{
    public string SequenceName { get; } = sequenceName;
}
