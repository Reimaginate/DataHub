using FluentAssertions;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Xunit;
using Reimaginate.DataHub.Test.Unit.Base;
using Reimaginate.Test.Framework;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class ChangeTrackingHelperTests : ScenarioUnitTestBase
{
    [Fact]
    public async Task StripBaseProperties_should_remove_datahub_metadata_and_preserve_alternate_keys()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(StripBaseProperties_should_remove_datahub_metadata_and_preserve_alternate_keys)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(StripBaseProperties_should_remove_datahub_metadata_and_preserve_alternate_keys))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var entity = new JObject
                    {
                        [nameof(DataHubEntity.id)] = "entity-1",
                        [nameof(DataHubEntity.entityType)] = "DHType",
                        [nameof(DataHubEntity.createdOn)] = DateTimeOffset.Parse("2024-01-01T00:00:00Z"),
                        [nameof(DataHubEntity.lastUpdated)] = DateTimeOffset.Parse("2024-01-02T00:00:00Z"),
                        [nameof(DataHubEntity.alternateKeys)] = new JArray(new JObject { ["Key"] = "src.type", ["Value"] = "source-1" }),
                        ["_etag"] = "etag",
                        ["_ts"] = 123,
                        ["_dnf"] = true,
                        ["Name"] = "Preserved"
                    };

                    var stripped = ChangeTrackingHelper.StripBaseProperties(entity);

                    stripped.ContainsKey(nameof(DataHubEntity.id)).Should().BeFalse();
                    stripped.ContainsKey(nameof(DataHubEntity.entityType)).Should().BeFalse();
                    stripped.ContainsKey(nameof(DataHubEntity.createdOn)).Should().BeFalse();
                    stripped.ContainsKey(nameof(DataHubEntity.lastUpdated)).Should().BeFalse();
                    stripped.ContainsKey("_etag").Should().BeFalse();
                    stripped.ContainsKey("_ts").Should().BeFalse();
                    stripped.ContainsKey("_dnf").Should().BeFalse();
                    stripped.Value<string>("Name").Should().Be("Preserved");
                    stripped[nameof(DataHubEntity.alternateKeys)].Should().NotBeNull();

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
    public async Task ReassembleEntity_should_apply_ordered_updates_and_ignore_updates_older_than_init()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(ReassembleEntity_should_apply_ordered_updates_and_ignore_updates_older_than_init)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(ReassembleEntity_should_apply_ordered_updates_and_ignore_updates_older_than_init))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var initTime = DateTimeOffset.Parse("2024-01-01T00:00:00Z");
                    var initData = new JObject
                    {
                        ["Name"] = "Initial",
                        ["Optional"] = "remove-me",
                        ["Tags"] = new JArray("one")
                    };

                    var olderPatch = Diff(
                        new JObject { ["Name"] = "Before" },
                        new JObject { ["Name"] = "Ignored" });

                    var firstPatch = Diff(
                        initData,
                        new JObject
                        {
                            ["Name"] = "Updated",
                            ["Optional"] = "remove-me",
                            ["Tags"] = new JArray("one", "two")
                        });

                    var secondPatch = Diff(
                        new JObject
                        {
                            ["Name"] = "Updated",
                            ["Optional"] = "remove-me",
                            ["Tags"] = new JArray("one", "two")
                        },
                        new JObject
                        {
                            ["Name"] = "Updated",
                            ["Tags"] = new JArray("one", "two")
                        });

                    var result = ChangeTrackingHelper.ReassembleEntity(
                    [
                        Entry(ChangeTrackingEntryTypes.Update, initTime.AddMinutes(-5), olderPatch),
                        Entry(ChangeTrackingEntryTypes.Init, initTime, initData),
                        Entry(ChangeTrackingEntryTypes.Update, initTime.AddMinutes(1), firstPatch),
                        Entry(ChangeTrackingEntryTypes.Update, initTime.AddMinutes(2), secondPatch)
                    ]);

                    result.Value<string>("Name").Should().Be("Updated");
                    result["Optional"].Should().BeNull();
                    result.Value<JArray>("Tags")!.Values<string>().Should().Equal("one", "two");
                    result.Value<string>(nameof(DataHubEntity.entityType)).Should().Be("DHType");
                    result.Value<string>(nameof(DataHubEntity.id)).Should().Be("entity-1");
                    result.DateTimeOffsetValueRequired(nameof(DataHubEntity.lastUpdated)).Should().Be(initTime.AddMinutes(2));

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
    public async Task ReassembleEntity_should_apply_same_timestamp_updates_by_cosmos_timestamp_when_available()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(ReassembleEntity_should_apply_same_timestamp_updates_by_cosmos_timestamp_when_available)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(ReassembleEntity_should_apply_same_timestamp_updates_by_cosmos_timestamp_when_available))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var timestamp = DateTimeOffset.Parse("2024-01-01T00:00:00Z");

                    var result = ChangeTrackingHelper.ReassembleEntity(
                    [
                        Entry(ChangeTrackingEntryTypes.Init, timestamp, new JObject { ["Name"] = "Initial" }),
                        Entry(ChangeTrackingEntryTypes.Update, timestamp, Diff(
                            new JObject { ["Name"] = "Initial" },
                            new JObject { ["Name"] = "First" }), ts: 10),
                        Entry(ChangeTrackingEntryTypes.Update, timestamp, Diff(
                            new JObject { ["Name"] = "First" },
                            new JObject { ["Name"] = "Second" }), ts: 20)
                    ]);

                    result.Value<string>("Name").Should().Be("Second");
                    result.DateTimeOffsetValueRequired(nameof(DataHubEntity.lastUpdated)).Should().Be(timestamp);

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
    public async Task ReassembleEntity_should_tolerate_patch_failures_when_requested()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(ReassembleEntity_should_tolerate_patch_failures_when_requested)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(ReassembleEntity_should_tolerate_patch_failures_when_requested))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var timestamp = DateTimeOffset.Parse("2024-01-01T00:00:00Z");
                    var invalidPatch = new JObject
                    {
                        ["Items"] = new JObject
                        {
                            ["_t"] = "a",
                            ["_0"] = new JArray("A", 0, 0)
                        }
                    };

                    var result = ChangeTrackingHelper.ReassembleEntity(
                    [
                        Entry(ChangeTrackingEntryTypes.Init, timestamp, new JObject { ["Name"] = "Initial", ["Items"] = "not-array" }),
                        Entry(ChangeTrackingEntryTypes.Update, timestamp.AddMinutes(1), invalidPatch)
                    ], skipPatchFailures: true);

                    result.Value<string>("Name").Should().Be("Initial");
                    result["Items"].Should().NotBeNull();

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
    public async Task DiffIgnoringEquivalentMixedDateTokens_should_ignore_same_instant_string_and_date_tokens()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(DiffIgnoringEquivalentMixedDateTokens_should_ignore_same_instant_string_and_date_tokens)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(DiffIgnoringEquivalentMixedDateTokens_should_ignore_same_instant_string_and_date_tokens))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var left = new JObject
                    {
                        ["ValueE"] = "2024-01-02T03:04:05.000+10:00"
                    };
                    var right = new JObject
                    {
                        ["ValueE"] = JToken.FromObject(DateTimeOffset.Parse("2024-01-02T03:04:05+10:00"))
                    };

                    var diff = ChangeTrackingHelper.DiffIgnoringEquivalentMixedDateTokens(left, right);

                    diff.Should().BeNull();
                    left["ValueE"]!.Type.Should().Be(JTokenType.String);
                    right["ValueE"]!.Type.Should().Be(JTokenType.Date);

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
    public async Task DiffIgnoringEquivalentMixedDateTokens_should_keep_different_mixed_date_values_as_updates()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(DiffIgnoringEquivalentMixedDateTokens_should_keep_different_mixed_date_values_as_updates)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(DiffIgnoringEquivalentMixedDateTokens_should_keep_different_mixed_date_values_as_updates))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var left = new JObject
                    {
                        ["ValueE"] = "2024-01-02T03:04:05.000+10:00"
                    };
                    var right = new JObject
                    {
                        ["ValueE"] = JToken.FromObject(DateTimeOffset.Parse("2024-01-02T03:04:06+10:00"))
                    };

                    var diff = (JObject)ChangeTrackingHelper.DiffIgnoringEquivalentMixedDateTokens(left, right);

                    diff.Should().NotBeNull();
                    diff["ValueE"].Should().BeOfType<JArray>();

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
    public async Task DiffIgnoringEquivalentMixedDateTokens_should_not_normalize_string_to_string_date_differences()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(DiffIgnoringEquivalentMixedDateTokens_should_not_normalize_string_to_string_date_differences)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(DiffIgnoringEquivalentMixedDateTokens_should_not_normalize_string_to_string_date_differences))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var left = new JObject
                    {
                        ["ValueE"] = "2024-01-02T03:04:05.000+10:00"
                    };
                    var right = new JObject
                    {
                        ["ValueE"] = "2024-01-02T03:04:05+10:00"
                    };

                    var diff = (JObject)ChangeTrackingHelper.DiffIgnoringEquivalentMixedDateTokens(left, right);

                    diff.Should().NotBeNull();
                    diff["ValueE"].Should().BeOfType<JArray>();

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
    public async Task DiffIgnoringEquivalentMixedDateTokens_should_keep_non_date_updates()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(DiffIgnoringEquivalentMixedDateTokens_should_keep_non_date_updates)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(DiffIgnoringEquivalentMixedDateTokens_should_keep_non_date_updates))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var left = new JObject
                    {
                        ["ValueA"] = "Old"
                    };
                    var right = new JObject
                    {
                        ["ValueA"] = "New"
                    };

                    var diff = (JObject)ChangeTrackingHelper.DiffIgnoringEquivalentMixedDateTokens(left, right);

                    diff.Should().NotBeNull();
                    diff["ValueA"]!.ToString(Newtonsoft.Json.Formatting.None).Should().Be("""["Old","New"]""");

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
    [MemberData(nameof(EquivalentNumericValues))]
    public async Task DiffIgnoringEquivalentValueTokens_should_ignore_equivalent_numeric_tokens(object leftValue, object rightValue)
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(DiffIgnoringEquivalentValueTokens_should_ignore_equivalent_numeric_tokens)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(DiffIgnoringEquivalentValueTokens_should_ignore_equivalent_numeric_tokens))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var left = new JObject
                    {
                        ["PriceSold"] = new JValue(leftValue)
                    };
                    var right = new JObject
                    {
                        ["PriceSold"] = new JValue(rightValue)
                    };
                    var leftBefore = left.DeepClone();
                    var rightBefore = right.DeepClone();

                    var diff = ChangeTrackingHelper.DiffIgnoringEquivalentValueTokens(left, right);

                    diff.Should().BeNull();
                    JToken.DeepEquals(left, leftBefore).Should().BeTrue();
                    JToken.DeepEquals(right, rightBefore).Should().BeTrue();

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
    public async Task DiffIgnoringEquivalentValueTokens_should_ignore_nested_object_and_array_numeric_equivalents()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(DiffIgnoringEquivalentValueTokens_should_ignore_nested_object_and_array_numeric_equivalents)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(DiffIgnoringEquivalentValueTokens_should_ignore_nested_object_and_array_numeric_equivalents))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var left = new JObject
                    {
                        ["Amount"] = new JValue(1),
                        ["Nested"] = new JObject
                        {
                            ["Amount"] = new JValue(2L)
                        },
                        ["Items"] = new JArray(new JValue(3), new JObject { ["Amount"] = new JValue(4m) })
                    };
                    var right = new JObject
                    {
                        ["Amount"] = new JValue(1.0d),
                        ["Nested"] = new JObject
                        {
                            ["Amount"] = new JValue(2m)
                        },
                        ["Items"] = new JArray(new JValue(3.0d), new JObject { ["Amount"] = new JValue(4.0d) })
                    };

                    var diff = ChangeTrackingHelper.DiffIgnoringEquivalentValueTokens(left, right);

                    diff.Should().BeNull();

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
    [MemberData(nameof(DifferentNumericValues))]
    public async Task DiffIgnoringEquivalentValueTokens_should_keep_real_numeric_updates(JToken leftValue, JToken rightValue)
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(DiffIgnoringEquivalentValueTokens_should_keep_real_numeric_updates)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(DiffIgnoringEquivalentValueTokens_should_keep_real_numeric_updates))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var left = new JObject
                    {
                        ["PriceSold"] = leftValue
                    };
                    var right = new JObject
                    {
                        ["PriceSold"] = rightValue
                    };

                    var diff = (JObject)ChangeTrackingHelper.DiffIgnoringEquivalentValueTokens(left, right);

                    diff.Should().NotBeNull();
                    diff["PriceSold"].Should().NotBeNull();
                    diff["PriceSold"].Should().BeOfType<JArray>();

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
    public async Task DiffIgnoringEquivalentValueTokens_should_keep_missing_property_numeric_updates()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(DiffIgnoringEquivalentValueTokens_should_keep_missing_property_numeric_updates)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(DiffIgnoringEquivalentValueTokens_should_keep_missing_property_numeric_updates))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var left = new JObject
                    {
                        ["PriceSold"] = new JValue(0)
                    };
                    var right = new JObject();

                    var diff = (JObject)ChangeTrackingHelper.DiffIgnoringEquivalentValueTokens(left, right);

                    diff.Should().NotBeNull();
                    diff["PriceSold"]!.Should().BeOfType<JArray>();

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
    [MemberData(nameof(NonNumericBoundaryValues))]
    public async Task DiffIgnoringEquivalentValueTokens_should_keep_non_numeric_boundary_updates(JToken leftValue, JToken rightValue)
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(DiffIgnoringEquivalentValueTokens_should_keep_non_numeric_boundary_updates)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(DiffIgnoringEquivalentValueTokens_should_keep_non_numeric_boundary_updates))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var left = new JObject
                    {
                        ["Value"] = leftValue
                    };
                    var right = new JObject
                    {
                        ["Value"] = rightValue
                    };

                    var diff = (JObject)ChangeTrackingHelper.DiffIgnoringEquivalentValueTokens(left, right);

                    diff.Should().NotBeNull();
                    diff["Value"]!.Should().BeOfType<JArray>();

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
    [MemberData(nameof(NonFiniteNumericValues))]
    public async Task DiffIgnoringEquivalentValueTokens_should_keep_unconvertible_numeric_updates(JValue leftValue)
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(DiffIgnoringEquivalentValueTokens_should_keep_unconvertible_numeric_updates)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(DiffIgnoringEquivalentValueTokens_should_keep_unconvertible_numeric_updates))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var left = new JObject
                    {
                        ["Value"] = leftValue
                    };
                    var right = new JObject
                    {
                        ["Value"] = new JValue(1)
                    };

                    var act = () => ChangeTrackingHelper.DiffIgnoringEquivalentValueTokens(left, right);

                    act.Should().NotThrow();
                    act().Should().NotBeNull();

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
    public async Task DiffIgnoringEquivalentValueTokens_should_ignore_same_instant_string_and_datetime_tokens()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(DiffIgnoringEquivalentValueTokens_should_ignore_same_instant_string_and_datetime_tokens)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(DiffIgnoringEquivalentValueTokens_should_ignore_same_instant_string_and_datetime_tokens))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var instant = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    var left = new JObject
                    {
                        ["ValueE"] = "2024-01-01T00:00:00.0000000Z"
                    };
                    var right = new JObject
                    {
                        ["ValueE"] = JToken.FromObject(instant)
                    };

                    var diff = ChangeTrackingHelper.DiffIgnoringEquivalentValueTokens(left, right);

                    diff.Should().BeNull();
                    left["ValueE"]!.Type.Should().Be(JTokenType.String);
                    right["ValueE"]!.Type.Should().Be(JTokenType.Date);

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
    public async Task DiffIgnoringEquivalentValueTokens_should_keep_date_token_to_non_date_string_updates()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(DiffIgnoringEquivalentValueTokens_should_keep_date_token_to_non_date_string_updates)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(DiffIgnoringEquivalentValueTokens_should_keep_date_token_to_non_date_string_updates))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var left = new JObject
                    {
                        ["ValueE"] = JToken.FromObject(DateTimeOffset.Parse("2024-01-01T00:00:00Z"))
                    };
                    var right = new JObject
                    {
                        ["ValueE"] = "not-a-date"
                    };

                    var diff = (JObject)ChangeTrackingHelper.DiffIgnoringEquivalentValueTokens(left, right);

                    diff.Should().NotBeNull();
                    diff["ValueE"]!.Should().BeOfType<JArray>();

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
    public async Task DiffIgnoringEquivalentValueTokens_should_keep_array_length_and_missing_property_updates()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(DiffIgnoringEquivalentValueTokens_should_keep_array_length_and_missing_property_updates)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(DiffIgnoringEquivalentValueTokens_should_keep_array_length_and_missing_property_updates))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var left = new JObject
                    {
                        ["Items"] = new JArray(new JValue(1), new JValue(2)),
                        ["Existing"] = new JValue(0)
                    };
                    var right = new JObject
                    {
                        ["Items"] = new JArray(new JValue(1.0d)),
                        ["New"] = new JValue(0)
                    };

                    var diff = (JObject)ChangeTrackingHelper.DiffIgnoringEquivalentValueTokens(left, right);

                    diff.Should().NotBeNull();
                    diff["Items"].Should().NotBeNull();
                    diff["Existing"].Should().NotBeNull();
                    diff["New"].Should().NotBeNull();

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
    public async Task DiffIgnoringEquivalentValueTokens_should_report_only_real_changes_in_mixed_nested_graphs()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(DiffIgnoringEquivalentValueTokens_should_report_only_real_changes_in_mixed_nested_graphs)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(DiffIgnoringEquivalentValueTokens_should_report_only_real_changes_in_mixed_nested_graphs))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var left = new JObject
                    {
                        ["Equivalent"] = new JValue(1),
                        ["Nested"] = new JObject
                        {
                            ["Equivalent"] = new JValue(2L),
                            ["Changed"] = "Before"
                        },
                        ["Items"] = new JArray(new JValue(3), new JValue(4))
                    };
                    var right = new JObject
                    {
                        ["Equivalent"] = new JValue(1.0d),
                        ["Nested"] = new JObject
                        {
                            ["Equivalent"] = new JValue(2m),
                            ["Changed"] = "After"
                        },
                        ["Items"] = new JArray(new JValue(3m), new JValue(5))
                    };

                    var diff = (JObject)ChangeTrackingHelper.DiffIgnoringEquivalentValueTokens(left, right);
                    var diffText = diff.ToString(Newtonsoft.Json.Formatting.None);

                    diffText.Should().NotContain("Equivalent");
                    diff["Nested"]!["Changed"].Should().NotBeNull();
                    diff["Items"].Should().NotBeNull();

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
    public async Task DiffIgnoringEquivalentValueTokens_should_produce_patchable_numeric_update_diffs()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(DiffIgnoringEquivalentValueTokens_should_produce_patchable_numeric_update_diffs)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(DiffIgnoringEquivalentValueTokens_should_produce_patchable_numeric_update_diffs))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var left = new JObject
                    {
                        ["PriceSold"] = new JValue(1)
                    };
                    var right = new JObject
                    {
                        ["PriceSold"] = new JValue(2L)
                    };

                    var diff = ChangeTrackingHelper.DiffIgnoringEquivalentValueTokens(left, right);
                    var patched = (JObject)ChangeTrackingHelper.JsonDiffPatch.Patch(left.DeepClone(), diff);

                    patched.Value<long>("PriceSold").Should().Be(2L);

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
    public async Task DiffIgnoringEquivalentValueTokens_should_preserve_unchanged_numeric_array_token_types_in_patchable_diffs()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(DiffIgnoringEquivalentValueTokens_should_preserve_unchanged_numeric_array_token_types_in_patchable_diffs)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(DiffIgnoringEquivalentValueTokens_should_preserve_unchanged_numeric_array_token_types_in_patchable_diffs))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var left = new JObject
                    {
                        ["ValueO"] = new JArray(new JValue((byte)1), new JValue((byte)2))
                    };
                    var right = new JObject
                    {
                        ["ValueO"] = new JArray(new JValue(1.0d), new JValue((byte)3))
                    };

                    var diff = ChangeTrackingHelper.DiffIgnoringEquivalentValueTokens(left, right);
                    var patched = (JObject)ChangeTrackingHelper.JsonDiffPatch.Patch(left.DeepClone(), diff);

                    patched["ValueO"]![0]!.Type.Should().Be(JTokenType.Integer);
                    patched["ValueO"]!.ToObject<List<byte>>().Should().Equal((byte)1, (byte)3);

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
    [InlineData("left")]
    [InlineData("right")]
    public async Task DiffIgnoringEquivalentValueTokens_should_handle_null_root_tokens(string nullSide)
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(DiffIgnoringEquivalentValueTokens_should_handle_null_root_tokens)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(DiffIgnoringEquivalentValueTokens_should_handle_null_root_tokens))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var left = nullSide == "left" ? null : new JObject { ["Value"] = 1 };
                    var right = nullSide == "right" ? null : new JObject { ["Value"] = 1 };

                    var act = () => ChangeTrackingHelper.DiffIgnoringEquivalentValueTokens(left, right);

                    act.Should().NotThrow();
                    act().Should().NotBeNull();

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
    [MemberData(nameof(NullAndUndefinedValues))]
    public async Task DiffIgnoringEquivalentValueTokens_should_keep_null_and_undefined_boundary_updates(JToken leftValue, JToken rightValue)
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(DiffIgnoringEquivalentValueTokens_should_keep_null_and_undefined_boundary_updates)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(DiffIgnoringEquivalentValueTokens_should_keep_null_and_undefined_boundary_updates))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var left = new JObject
                    {
                        ["Value"] = leftValue
                    };
                    var right = new JObject
                    {
                        ["Value"] = rightValue
                    };

                    var diff = ChangeTrackingHelper.DiffIgnoringEquivalentValueTokens(left, right);

                    diff.Should().NotBeNull();

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
    public async Task DiffIgnoringEquivalentValueTokens_should_not_mutate_deep_input_graphs()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(DiffIgnoringEquivalentValueTokens_should_not_mutate_deep_input_graphs)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(DiffIgnoringEquivalentValueTokens_should_not_mutate_deep_input_graphs))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var left = new JObject
                    {
                        ["Date"] = "2024-01-01T00:00:00Z",
                        ["Number"] = new JValue(1),
                        ["Nested"] = new JObject
                        {
                            ["Number"] = new JValue(2L)
                        },
                        ["Array"] = new JArray(new JValue(3), "same")
                    };
                    var right = new JObject
                    {
                        ["Date"] = JToken.FromObject(DateTimeOffset.Parse("2024-01-01T00:00:00Z")),
                        ["Number"] = new JValue(1.0d),
                        ["Nested"] = new JObject
                        {
                            ["Number"] = new JValue(2m)
                        },
                        ["Array"] = new JArray(new JValue(3.0d), "same")
                    };
                    var leftBefore = left.DeepClone();
                    var rightBefore = right.DeepClone();

                    var diff = ChangeTrackingHelper.DiffIgnoringEquivalentValueTokens(left, right);

                    diff.Should().BeNull();
                    JToken.DeepEquals(left, leftBefore).Should().BeTrue();
                    JToken.DeepEquals(right, rightBefore).Should().BeTrue();

                    return await ActionResult(currentObject, stash);
                });
            })
            .Then("The assertions complete successfully", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .Run();
    }

    public static TheoryData<object, object> EquivalentNumericValues()
    {
        return new TheoryData<object, object>
        {
            { (sbyte)0, (short)0 },
            { (sbyte)1, (int)1 },
            { (short)-1, (long)-1 },
            { (byte)1, (ushort)1 },
            { (ushort)1, (uint)1 },
            { (uint)1, (ulong)1 },
            { 1, 1L },
            { 1, 1m },
            { 1, 1.0f },
            { 1, 1.0d },
            { 1.5f, 1.5d },
            { 1.5d, 1.5m },
            { 9007199254740991L, 9007199254740991m },
            { 1m, 1.0m },
            { 1.0m, 1.00m }
        };
    }

    public static TheoryData<JToken, JToken> DifferentNumericValues()
    {
        return new TheoryData<JToken, JToken>
        {
            { new JValue(1), new JValue(2) },
            { new JValue(1), new JValue(1.0001m) },
            { new JValue(-1), new JValue(1) },
            { new JValue(0), JValue.CreateNull() }
        };
    }

    public static TheoryData<JToken, JToken> NonNumericBoundaryValues()
    {
        return new TheoryData<JToken, JToken>
        {
            { new JValue("1"), new JValue(1) },
            { new JValue("1.0"), new JValue(1.0d) },
            { new JValue(true), new JValue(1) },
            { new JValue(false), new JValue(0) },
            { new JValue(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")), new JValue("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa") },
            { new JValue(new Uri("https://example.com/1")), new JValue("https://example.com/1") }
        };
    }

    public static TheoryData<JValue> NonFiniteNumericValues()
    {
        return new TheoryData<JValue>
        {
            new JValue(double.NaN),
            new JValue(double.PositiveInfinity),
            new JValue(double.NegativeInfinity),
            new JValue(float.NaN),
            new JValue(float.PositiveInfinity),
            new JValue(float.NegativeInfinity)
        };
    }

    public static TheoryData<JToken, JToken> NullAndUndefinedValues()
    {
        return new TheoryData<JToken, JToken>
        {
            { JValue.CreateNull(), new JValue(0) },
            { JValue.CreateUndefined(), new JValue(0) },
            { JValue.CreateNull(), JValue.CreateUndefined() }
        };
    }

    private static JObject Diff(JObject left, JObject right)
    {
        return (JObject)ChangeTrackingHelper.JsonDiffPatch.Diff(left, right);
    }

    private static ChangeTrackingEntry Entry(string entryType, DateTimeOffset timestamp, JObject data, long ts = 0)
    {
        return new ChangeTrackingEntry
        {
            DataSource = "SRC1",
            EntityType = "DHType",
            EntityId = "entity-1",
            EntryType = entryType,
            Timestamp = timestamp,
            Data = (JObject)data.DeepClone(),
            _ts = ts
        };
    }
}
