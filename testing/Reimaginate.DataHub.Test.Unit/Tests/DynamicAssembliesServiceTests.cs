using FluentAssertions;
using Reimaginate.DataHub.Services.DynamicAssemblies;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Interfaces;
using Xunit;
using Reimaginate.DataHub.Test.Unit.Base;
using Reimaginate.Test.Framework;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class DynamicAssembliesServiceTests : ScenarioUnitTestBase
{
    [Fact]
    public async Task LoadAssembly_should_include_generated_rule_body_in_cache_key()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(LoadAssembly_should_include_generated_rule_body_in_cache_key)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(LoadAssembly_should_include_generated_rule_body_in_cache_key))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    using var service = new DynamicAssembliesService();

                    var activeResolver = service.LoadAssemblyAndReturnType<IPreMergeRuleResolver>(
                        DynamicAssemblyTypes.PreMergeRuleResolver,
                        "PreMergeRuleResolver.DHType.0",
                        ["incoming.Value<string>(\"RecordState\") == \"Active\""],
                        "PreMergeRuleResolver");

                    var inactiveResolver = service.LoadAssemblyAndReturnType<IPreMergeRuleResolver>(
                        DynamicAssemblyTypes.PreMergeRuleResolver,
                        "PreMergeRuleResolver.DHType.0",
                        ["incoming.Value<string>(\"RecordState\") == \"Inactive\""],
                        "PreMergeRuleResolver");

                    var incoming = new Newtonsoft.Json.Linq.JObject
                    {
                        ["RecordState"] = "Active"
                    };

                    activeResolver.Resolve(incoming, null!, null!).Should().BeTrue();
                    inactiveResolver.Resolve(incoming, null!, null!).Should().BeFalse();
                    inactiveResolver.GetType().Assembly.Should().NotBeSameAs(activeResolver.GetType().Assembly);

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
    public async Task LoadAssembly_should_compile_duplicate_resolver_with_formatted_query_body()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(LoadAssembly_should_compile_duplicate_resolver_with_formatted_query_body)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(LoadAssembly_should_compile_duplicate_resolver_with_formatted_query_body))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    using var service = new DynamicAssembliesService();
                    var find = new Requests.Internal.FindPotentialDuplicates.FindPotentialDuplicatesRequestHandler(null!, null!)
                        .Detokenize("x.RecordState = $Active and @In<string>($ValueA)");

                    var resolver = service.LoadAssemblyAndReturnType<IDuplicateResolver>(
                        DynamicAssemblyTypes.DuplicateResolver,
                        "DuplicateResolver.DHType",
                        [
                            find,
                            "Equals<string>(pd,i,\"ValueA\")"
                        ],
                        "DuplicateResolver");

                    resolver.Should().NotBeNull();

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
