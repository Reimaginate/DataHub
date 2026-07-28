using FluentAssertions;
using Reimaginate.DataHub.Helpers;
using Xunit;
using Reimaginate.DataHub.Test.Unit.Base;
using Reimaginate.Test.Framework;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class StringExtensionsTests : ScenarioUnitTestBase
{
    [Fact]
    public async Task EscapeCosmosSpecialChars_should_escape_single_quotes_used_in_query_literals()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(EscapeCosmosSpecialChars_should_escape_single_quotes_used_in_query_literals)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(EscapeCosmosSpecialChars_should_escape_single_quotes_used_in_query_literals))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    "SRC'1".EscapeCosmosSpecialChars().Should().Be("SRC\\'1");

                    return await ActionResult(currentObject, stash);
                });
            })
            .Then("The assertions complete successfully", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .Run();
    }

    [Fact]
    public async Task EscapeCosmosSpecialChars_should_preserve_null_and_strings_without_quotes()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(EscapeCosmosSpecialChars_should_preserve_null_and_strings_without_quotes)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(EscapeCosmosSpecialChars_should_preserve_null_and_strings_without_quotes))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    ((string?)null).EscapeCosmosSpecialChars().Should().BeNull();
                    "SRC1".EscapeCosmosSpecialChars().Should().Be("SRC1");

                    return await ActionResult(currentObject, stash);
                });
            })
            .Then("The assertions complete successfully", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .Run();
    }
}
