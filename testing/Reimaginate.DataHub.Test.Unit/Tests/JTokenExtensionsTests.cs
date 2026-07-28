using FluentAssertions;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.Helpers;
using Xunit;
using Reimaginate.DataHub.Test.Unit.Base;
using Reimaginate.Test.Framework;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class JTokenExtensionsTests : ScenarioUnitTestBase
{
    [Theory]
    [MemberData(nameof(EmptyTokens))]
    public async Task IsEmpty_should_identify_null_empty_object_and_empty_array(JToken? token)
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(IsEmpty_should_identify_null_empty_object_and_empty_array)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(IsEmpty_should_identify_null_empty_object_and_empty_array))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    token.IsEmpty().Should().BeTrue();

                    return await ActionResult(currentObject, stash);
                });
            })
            .Then("The assertions complete successfully", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .Run();
    }

    [Theory]
    [MemberData(nameof(NonEmptyTokens))]
    public async Task IsEmpty_should_not_treat_default_scalar_values_as_empty(JToken token)
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(IsEmpty_should_not_treat_default_scalar_values_as_empty)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(IsEmpty_should_not_treat_default_scalar_values_as_empty))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    token.IsEmpty().Should().BeFalse();

                    return await ActionResult(currentObject, stash);
                });
            })
            .Then("The assertions complete successfully", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .Run();
    }

    public static IEnumerable<object?[]> EmptyTokens()
    {
        yield return [null];
        yield return [JValue.CreateNull()];
        yield return [new JObject()];
        yield return [new JArray()];
    }

    public static IEnumerable<object[]> NonEmptyTokens()
    {
        yield return [new JValue(0)];
        yield return [new JValue(false)];
        yield return [new JValue("")];
        yield return [new JObject { ["Value"] = JValue.CreateNull() }];
        yield return [new JArray { JValue.CreateNull() }];
    }
}
