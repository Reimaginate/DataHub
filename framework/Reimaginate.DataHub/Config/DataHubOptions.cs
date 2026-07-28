namespace Reimaginate.DataHub.Config;

public class DataHubOptions
{
    public DataStoreOptions DataStoreOptions { get; set; } = new();
    public ProcessingLockOptions ProcessingLockOptions { get; set; } = new();
    public NotificationServiceOptions NotificationServiceOptions { get; set; } = new();
    public TaskHubOptions TaskHubOptions { get; set; }

}