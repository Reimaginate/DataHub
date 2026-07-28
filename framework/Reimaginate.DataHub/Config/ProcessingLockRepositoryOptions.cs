using System;

namespace Reimaginate.DataHub.Config;

public class ProcessingLockRepositoryOptions
{
    public Func<AddDataHubServiceOptions> UseInMemoryRepository { get; set; }
    public Func<Action<ProcessingLockRepositoryOptions>, AddDataHubServiceOptions> UseRedisRepository { get; set; }
}