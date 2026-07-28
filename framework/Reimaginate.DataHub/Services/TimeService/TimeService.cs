using System;

namespace Reimaginate.DataHub.Services.TimeService;

public interface ITimeService
{
    DateTimeOffset Now();
}

public class TimeService : ITimeService
{
    public DateTimeOffset Now()
    {
        return DateTimeOffset.Now;
    }
}