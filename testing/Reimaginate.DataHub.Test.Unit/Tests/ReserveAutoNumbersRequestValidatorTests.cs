using FluentAssertions;
using Reimaginate.DataHub.Requests.External.Client.ReserveAutoNumbers;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Xunit;
using Reimaginate.DataHub.Test.Unit.Base;
using Reimaginate.Test.Framework;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class ReserveAutoNumbersRequestValidatorTests : ScenarioUnitTestBase
{
    [Fact]
    public async Task Validator_should_require_sequence_name_and_count_range()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Validator_should_require_sequence_name_and_count_range)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Validator_should_require_sequence_name_and_count_range))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var validator = new ReserveAutoNumbersRequestValidator();

                    var result = validator.Validate(new ReserveAutoNumbersRequest
                    {
                        SequenceName = "",
                        Count = 0
                    });

                    result.IsValid.Should().BeFalse();
                    result.Errors.Should().Contain(e => e.PropertyName == nameof(ReserveAutoNumbersRequest.SequenceName));
                    result.Errors.Should().Contain(e => e.PropertyName == nameof(ReserveAutoNumbersRequest.Count));

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
    public async Task Validator_should_accept_maximum_batch_size()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Validator_should_accept_maximum_batch_size)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Validator_should_accept_maximum_batch_size))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var validator = new ReserveAutoNumbersRequestValidator();

                    var result = validator.Validate(new ReserveAutoNumbersRequest
                    {
                        SequenceName = "orders",
                        Count = ReserveAutoNumbersRequestValidator.MaxCount
                    });

                    result.IsValid.Should().BeTrue();

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
