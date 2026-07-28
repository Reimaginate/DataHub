using FluentAssertions;
using NSubstitute;
using Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocuments;
using Reimaginate.DataHub.Services.EntityConfig;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;
using Xunit;
using Reimaginate.DataHub.Test.Unit.Base;
using Reimaginate.Test.Framework;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class EntityConfigServiceTests : ScenarioUnitTestBase
{
    [Fact]
    public async Task GetEntityConfig_should_not_cache_missing_results_as_null()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(GetEntityConfig_should_not_cache_missing_results_as_null)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(GetEntityConfig_should_not_cache_missing_results_as_null))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var mediator = Substitute.For<IMediator>();
                    var queryCount = 0;

                    mediator.TrySend<PagedResults<EntityConfig>>(
                            Arg.Any<GetCosmosDocumentsQuery<EntityConfig>>(),
                            Arg.Any<CancellationToken>(),
                            Arg.Any<Action<Exception>?>())
                        .Returns(_ =>
                        {
                            queryCount++;
                            var results = queryCount == 1
                                ? []
                                : new List<EntityConfig> { new() { id = "config-1", EntityType = "DHType" } };

                            Task<(PagedResults<EntityConfig>? Response, Exception? Exception)> response = Task.FromResult<(PagedResults<EntityConfig>? Response, Exception? Exception)>((new PagedResults<EntityConfig>
                            {
                                Results = results
                            }, null));
                            return response;
                        });

                    var service = new EntityConfigService(mediator);

                    var first = await service.GetEntityConfig("DHType", CancellationToken.None);
                    var second = await service.GetEntityConfig("DHType", CancellationToken.None);

                    first.Should().BeNull();
                    second.Should().NotBeNull();
                    second!.EntityType.Should().Be("DHType");
                    queryCount.Should().Be(2);

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
