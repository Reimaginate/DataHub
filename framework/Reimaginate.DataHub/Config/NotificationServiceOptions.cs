namespace Reimaginate.DataHub.Config;

public class NotificationServiceOptions
{
    public string UseMessagingService { get; set; }
    public AzureEventGridClientOptions AzureEventGridClientOptions { get; set; }
}