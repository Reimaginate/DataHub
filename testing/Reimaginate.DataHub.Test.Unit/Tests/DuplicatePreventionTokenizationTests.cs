using FluentAssertions;
using Reimaginate.DataHub.Requests.Internal.FindPotentialDuplicates;
using Xunit;
using Reimaginate.DataHub.Test.Unit.Base;
using Reimaginate.Test.Framework;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class DuplicatePreventionTokenizationTests : ScenarioUnitTestBase
{
    [Fact]
    public async Task Detokenize_should_lowercase_string_and_char_fields_but_not_numeric_fields()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Detokenize_should_lowercase_string_and_char_fields_but_not_numeric_fields)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Detokenize_should_lowercase_string_and_char_fields_but_not_numeric_fields))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var handler = new FindPotentialDuplicatesRequestHandler(null!, null!);

                    var output = handler.Detokenize("x.RecordState = $Active and @In<string>($ValueA) and @In<char>($ValueI) and @In<int>($ValueB) and @PathIn<int>($PhysicalAddress.PostCode)");

                    output.Should().Contain("x.RecordState = 'Active'");
                    output.Should().Contain("LOWER(x.ValueA) in ({In<string>(i, \"ValueA\")})");
                    output.Should().Contain("LOWER(x.ValueI) in ({In<char>(i, \"ValueI\")})");
                    output.Should().Contain("x.ValueB in ({In<int>(i, \"ValueB\")})");
                    output.Should().Contain("x.PhysicalAddress.PostCode in ({PathIn<int>(i, \"PhysicalAddress.PostCode\")})");
                    output.Should().NotContain("LOWER(x.ValueB)");
                    output.Should().NotContain("LOWER(x.PhysicalAddress.PostCode)");

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
    public async Task Detokenize_should_normalize_legacy_escaped_double_quoted_literals()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Detokenize_should_normalize_legacy_escaped_double_quoted_literals)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Detokenize_should_normalize_legacy_escaped_double_quoted_literals))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var handler = new FindPotentialDuplicatesRequestHandler(null!, null!);

                    var output = handler.Detokenize("x.RecordState = \\\"Active\\\" and @In<string>($ValueA)");

                    output.Should().Contain("x.RecordState = 'Active'");
                    output.Should().NotContain("\\\"Active\\\"");

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
