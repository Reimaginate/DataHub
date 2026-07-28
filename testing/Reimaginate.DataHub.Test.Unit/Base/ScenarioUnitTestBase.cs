using Microsoft.Extensions.DependencyInjection;
using Reimaginate.Test.Framework;
using Xunit;

namespace Reimaginate.DataHub.Test.Unit.Base;

public abstract class ScenarioUnitTestBase : IDisposable
{
    protected ScenarioUnitTestBase()
    {
        ServiceProvider = new ServiceCollection().BuildServiceProvider();
    }

    protected ServiceProvider ServiceProvider { get; }

    protected string? TestDisplayName([System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
    {
        var memberInfo = GetType().GetMember(memberName).FirstOrDefault();
        if (memberInfo == null) return null;

        var factAtt = memberInfo.GetCustomAttributes(typeof(FactAttribute), true).FirstOrDefault();
        return ((FactAttribute?)factAtt)?.DisplayName;
    }

    protected static Task<ScenarioActionResult> ActionResult(object currentObject, Dictionary<string, object?> stash) =>
        Task.FromResult(new ScenarioActionResult
        {
            CurrentObject = currentObject,
            Outputs = stash
        });

    protected static string Humanize(string value)
    {
        return value
            .Replace("_should_", " should ", StringComparison.OrdinalIgnoreCase)
            .Replace("_", " ", StringComparison.Ordinal)
            .Trim();
    }

    public void Dispose()
    {
        ServiceProvider.Dispose();
    }
}
