using System.Reflection;

namespace Reimaginate.DataHub.TestFramework.Helpers;

public class ServiceCall
{
    public object? Mock { get; set; }
    public MethodInfo Method { get; set; } = null!;
    public string MethodName { get; set; } = string.Empty;
    public object?[] Args { get; set; } = [];
    public object? Response { get; set; }

}
