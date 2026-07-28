using Reimaginate.RedisClient;

namespace Reimaginate.DataHub.Config;

public class ProcessingLockOptions
{
    public string UseRepository { get; set; } = "InMemory";
    public int WaitTimeout { get; set; } = 120;
    public int LockTimeout { get; set; } = 300;
    public RedisClientOptions RedisClientOptions { get; set; } = new();
}