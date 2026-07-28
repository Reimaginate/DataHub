using System;

namespace Reimaginate.DataHub.Services.IdService;

public interface IIdService
{
    string NewId<T>();
    string NewId(Type forType);
}

public class IdService : IIdService
{
    public string NewId<T>()
    {
        return NewId(typeof(T));
    }

    public string NewId(Type forType)
    {
        return Guid.NewGuid().ToString();
    }
}