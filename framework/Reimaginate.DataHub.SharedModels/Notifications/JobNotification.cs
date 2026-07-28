namespace Reimaginate.DataHub.SharedModels.Notifications;

public class JobNotification : Notification
{
    public JobNotification()
    {
        Subject = "JobNotification";
        EventType = nameof(JobNotification);
    }

    public string JobId { get; set; }
    public string JobType { get; set; }
    public string JobTarget { get; set; }
}