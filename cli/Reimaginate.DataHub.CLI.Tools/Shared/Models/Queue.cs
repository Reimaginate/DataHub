using System.Collections.Concurrent;

namespace Reimaginate.DataHub.CLI.Tools.Shared.Models;

public static class Queue
{
    public static readonly ConcurrentQueue<Blob> Items = new();
}