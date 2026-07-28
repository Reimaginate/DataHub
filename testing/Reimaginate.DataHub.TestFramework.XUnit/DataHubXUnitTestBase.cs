using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Reimaginate.DataHub.Config;
using Xunit;

namespace Reimaginate.DataHub.TestFramework.XUnit;

public class DataHubXUnitTestBase(Action<IConfigurationBuilder> configurationBuilder, Action<IServiceCollection, IConfiguration>? configureAgentServices = null, Func<AddDataHubServiceOptions, IConfiguration, AddDataHubServiceOptions>? dataHubOptions = null)
    : DataHubTestBase(configurationBuilder, configureAgentServices, dataHubOptions)
{
    protected string? TestDisplayName([System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
    {
        var memberInfo = GetType().GetMember(memberName).FirstOrDefault();
        if (memberInfo == null) return null;

        var factAtt = memberInfo.GetCustomAttributes(typeof(FactAttribute), true).FirstOrDefault();
        return ((FactAttribute)factAtt!)?.DisplayName;
    }
}