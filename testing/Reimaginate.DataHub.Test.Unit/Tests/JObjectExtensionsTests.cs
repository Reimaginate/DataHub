using FluentAssertions;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.SharedModels.Core;
using Xunit;
using Reimaginate.DataHub.Test.Unit.Base;
using Reimaginate.Test.Framework;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class JObjectExtensionsTests : ScenarioUnitTestBase
{
    [Fact]
    public async Task GetAllProperties_should_flatten_nested_object_paths_without_expanding_arrays()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(GetAllProperties_should_flatten_nested_object_paths_without_expanding_arrays)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(GetAllProperties_should_flatten_nested_object_paths_without_expanding_arrays))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var entity = new JObject
                    {
                        ["Name"] = "Example",
                        ["Nested"] = new JObject
                        {
                            ["Child"] = "Value",
                            ["Deeper"] = new JObject
                            {
                                ["Number"] = 42
                            }
                        },
                        ["Items"] = new JArray("A", "B")
                    };

                    var properties = entity.GetAllProperties();

                    properties.Keys.Should().BeEquivalentTo("Name", "Nested.Child", "Nested.Deeper.Number", "Items");
                    properties["Nested.Child"].Value<string>().Should().Be("Value");
                    properties["Nested.Deeper.Number"].Value<int>().Should().Be(42);
                    properties["Items"].Should().BeOfType<JArray>();

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
    public async Task SetProperty_should_create_missing_nested_objects_and_replace_existing_values()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(SetProperty_should_create_missing_nested_objects_and_replace_existing_values)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(SetProperty_should_create_missing_nested_objects_and_replace_existing_values))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var entity = new JObject
                    {
                        ["Existing"] = new JObject
                        {
                            ["Value"] = "Old"
                        }
                    };

                    entity.SetProperty("Existing.Value", "New");
                    entity.SetProperty("Created.Child.Value", 123);

                    entity.SelectToken("Existing.Value")!.Value<string>().Should().Be("New");
                    entity.SelectToken("Created.Child.Value")!.Value<int>().Should().Be(123);

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
    public async Task RemoveProperty_should_remove_nested_property_and_array_element_when_path_targets_index()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(RemoveProperty_should_remove_nested_property_and_array_element_when_path_targets_index)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(RemoveProperty_should_remove_nested_property_and_array_element_when_path_targets_index))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var entity = new JObject
                    {
                        ["Nested"] = new JObject
                        {
                            ["Keep"] = true,
                            ["Remove"] = "gone"
                        },
                        ["Items"] = new JArray("A", "B", "C")
                    };

                    entity.RemoveProperty("Nested.Remove");
                    entity.RemoveProperty("Items.[1]");

                    entity.SelectToken("Nested.Remove").Should().BeNull();
                    entity.SelectToken("Nested.Keep")!.Value<bool>().Should().BeTrue();
                    entity.Value<JArray>("Items")!.Values<string>().Should().Equal("A", "C");

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
    public async Task RemoveNullValues_should_remove_null_members_recursively()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(RemoveNullValues_should_remove_null_members_recursively)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(RemoveNullValues_should_remove_null_members_recursively))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var entity = new JObject
                    {
                        ["Keep"] = "Value",
                        ["Remove"] = JValue.CreateNull(),
                        ["Nested"] = new JObject
                        {
                            ["Keep"] = 1,
                            ["Remove"] = JValue.CreateNull()
                        },
                        ["Items"] = new JArray("A", JValue.CreateNull(), new JObject
                        {
                            ["Remove"] = JValue.CreateNull(),
                            ["Keep"] = "B"
                        })
                    };

                    var cleaned = entity.RemoveNullValues();

                    cleaned.SelectToken("Remove").Should().BeNull();
                    cleaned.SelectToken("Nested.Remove").Should().BeNull();
                    cleaned.SelectToken("Nested.Keep")!.Value<int>().Should().Be(1);
                    cleaned.Value<JArray>("Items")!.Should().HaveCount(2);
                    cleaned.SelectToken("Items[1].Keep")!.Value<string>().Should().Be("B");

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
    public async Task ExternalEntityReferences_should_find_top_level_nested_and_array_references()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(ExternalEntityReferences_should_find_top_level_nested_and_array_references)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(ExternalEntityReferences_should_find_top_level_nested_and_array_references))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var entity = new JObject
                    {
                        ["TopLevel"] = Reference("A"),
                        ["Nested"] = new JObject
                        {
                            ["Child"] = Reference("B")
                        },
                        ["Children"] = new JArray
                        {
                            new JObject { ["Reference"] = Reference("C") }
                        }
                    };

                    var references = entity.ExternalEntityReferences();

                    references.Select(r => r.Value<string>(nameof(EntityReference.EntityId)))
                        .Should()
                        .BeEquivalentTo(["A", "B", "C"]);

                    return await ActionResult(currentObject, stash);
                });
            })
            .Then("The assertions complete successfully", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .Run();
    }

    private static JObject Reference(string id)
    {
        return JObject.FromObject(new ExternalEntityReference
        {
            DataSource = "SRC",
            EntityType = "DHType",
            SourceEntityType = "TypeA",
            EntityId = id
        });
    }
}
