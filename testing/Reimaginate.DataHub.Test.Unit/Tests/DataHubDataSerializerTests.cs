using System.Text;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.SharedModels.Markers;
using Xunit;
using System.Globalization;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.Test.Unit.Base;
using Reimaginate.Test.Framework;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class DataHubDataSerializerTests : ScenarioUnitTestBase
{
    private const string DateLikeMarkerValue = "2026-05-27T17:00:51.0000000+10:00";
    private const string ReportedLastRunTimeValue = "2026-05-28T11:00:21.8979541+10:00";
    private static readonly DateTimeOffset LastRunTime = DateTimeOffset.Parse("2026-05-28T11:00:21.8979541+10:00");

    [Fact]
    public async Task FromStream_should_preserve_reported_existing_merge_marker_document()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(FromStream_should_preserve_reported_existing_merge_marker_document)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(FromStream_should_preserve_reported_existing_merge_marker_document))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var serializer = new DataHubDataSerializer();
                    using var stream = CreateStream($$"""
                    {
                      "AgentId": "D365-AGENT",
                      "DataSource": "D365",
                      "EntityType": "contact",
                      "Value": "{{DateLikeMarkerValue}}",
                      "LastRunTime": "{{ReportedLastRunTimeValue}}",
                      "_dt": "MergeMarker",
                      "pk": "",
                      "id": "0dffba44-165f-4776-bbe0-500768e89979",
                      "_etag": "\"3d00cb36-0000-1a00-0000-6a1793bf0000\"",
                      "_ts": 1779930047
                    }
                    """);

                    var marker = serializer.FromStream<MergeMarker>(stream);

                    marker.id.Should().Be("0dffba44-165f-4776-bbe0-500768e89979");
                    marker._dt.Should().Be(nameof(MergeMarker));
                    marker.pk.Should().BeEmpty();
                    marker._etag.Should().Be("\"3d00cb36-0000-1a00-0000-6a1793bf0000\"");
                    marker._ts.Should().Be(1779930047);
                    marker.Value.Should().Be(DateLikeMarkerValue);
                    marker.LastRunTime.Should().Be(LastRunTime);

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
    [MemberData(nameof(DateLikeStringValues))]
    public async Task FromStream_should_preserve_merge_marker_values_that_look_like_dates(string markerValue)
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(FromStream_should_preserve_merge_marker_values_that_look_like_dates)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(FromStream_should_preserve_merge_marker_values_that_look_like_dates))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var serializer = new DataHubDataSerializer();
                    using var stream = CreateStream($$"""
                    {
                      "id": "merge-marker-1",
                      "_dt": "MergeMarker",
                      "AgentId": "agent-1",
                      "DataSource": "SRC1",
                      "EntityType": "TypeA",
                      "Value": "{{markerValue}}",
                      "LastRunTime": "{{ReportedLastRunTimeValue}}",
                      "pk": ""
                    }
                    """);

                    var marker = serializer.FromStream<MergeMarker>(stream);

                    marker.Value.Should().Be(markerValue);
                    marker.LastRunTime.Should().Be(LastRunTime);

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
    public async Task FromStream_should_preserve_sync_marker_values_that_look_like_dates()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(FromStream_should_preserve_sync_marker_values_that_look_like_dates)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(FromStream_should_preserve_sync_marker_values_that_look_like_dates))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var serializer = new DataHubDataSerializer();
                    using var stream = CreateStream($$"""
                    {
                      "id": "sync-marker-1",
                      "_dt": "SyncMarker",
                      "AgentId": "agent-1",
                      "DataSource": "SRC1",
                      "EntityType": "DHType",
                      "Value": "{{DateLikeMarkerValue}}",
                      "LastRunTime": "{{ReportedLastRunTimeValue}}",
                      "pk": ""
                    }
                    """);

                    var marker = serializer.FromStream<SyncMarker>(stream);

                    marker.Value.Should().Be(DateLikeMarkerValue);
                    marker.LastRunTime.Should().Be(LastRunTime);

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
    public async Task FromStream_should_leave_date_like_jobject_values_as_strings()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(FromStream_should_leave_date_like_jobject_values_as_strings)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(FromStream_should_leave_date_like_jobject_values_as_strings))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var serializer = new DataHubDataSerializer();
                    using var stream = CreateStream($$"""
                    {
                      "Value": "{{DateLikeMarkerValue}}",
                      "LastRunTime": "{{ReportedLastRunTimeValue}}",
                      "DateOnly": "2026-05-27",
                      "UtcDateTime": "2026-05-27T07:00:51Z",
                      "Nested": {
                        "Value": "2026-05-27T17:00:51.0000000+10:00"
                      },
                      "Items": [
                        { "Value": "2026-05-27T17:00:51.0000000+10:00" }
                      ]
                    }
                    """);

                    var document = serializer.FromStream<JObject>(stream);

                    document["Value"]!.Type.Should().Be(JTokenType.String);
                    document.Value<string>("Value").Should().Be(DateLikeMarkerValue);
                    document["LastRunTime"]!.Type.Should().Be(JTokenType.String);
                    document["LastRunTime"]!.AsDateTimeOffset().Should().Be(LastRunTime);
                    document["DateOnly"]!.Type.Should().Be(JTokenType.String);
                    document["UtcDateTime"]!.Type.Should().Be(JTokenType.String);
                    document["Nested"]!["Value"]!.Type.Should().Be(JTokenType.String);
                    document["Items"]![0]!["Value"]!.Type.Should().Be(JTokenType.String);

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
    public async Task FromStream_should_not_use_current_culture_when_preserving_string_values()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(FromStream_should_not_use_current_culture_when_preserving_string_values)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(FromStream_should_not_use_current_culture_when_preserving_string_values))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var originalCulture = CultureInfo.CurrentCulture;
                    var originalUiCulture = CultureInfo.CurrentUICulture;

                    try
                    {
                        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-AU");
                        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-AU");
                        var serializer = new DataHubDataSerializer();
                        using var stream = CreateStream($$"""
                        {
                          "Value": "{{DateLikeMarkerValue}}",
                          "LastRunTime": "{{ReportedLastRunTimeValue}}"
                        }
                        """);

                        var document = serializer.FromStream<JObject>(stream);

                        document.Value<string>("Value").Should().Be(DateLikeMarkerValue);
                        document.Value<string>("Value").Should().NotBe("27/05/2026 17:00:51 +10:00");
                        document.Value<string>("Value").Should().NotBe("05/27/2026 17:00:51 +10:00");
                    }
                    finally
                    {
                        CultureInfo.CurrentCulture = originalCulture;
                        CultureInfo.CurrentUICulture = originalUiCulture;
                    }

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
    public async Task ToStream_should_continue_writing_datetime_offsets_with_configured_iso_format()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(ToStream_should_continue_writing_datetime_offsets_with_configured_iso_format)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(ToStream_should_continue_writing_datetime_offsets_with_configured_iso_format))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var serializer = new DataHubDataSerializer();
                    var marker = new MergeMarker
                    {
                        id = "merge-marker-1",
                        AgentId = "D365-AGENT",
                        DataSource = "D365",
                        EntityType = "contact",
                        Value = DateLikeMarkerValue,
                        LastRunTime = LastRunTime,
                        pk = ""
                    };

                    var document = SerializeToJObject(serializer, marker);
                    var lastRunTime = document.Value<string>("LastRunTime");

                    document.Value<string>("Value").Should().Be(DateLikeMarkerValue);
                    lastRunTime.Should().EndWith("+10:00");
                    DateTimeOffset.Parse(lastRunTime!).Should().Be(LastRunTime);

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
    public async Task RoundTrip_should_not_mutate_reported_existing_marker_value_or_last_run_time()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(RoundTrip_should_not_mutate_reported_existing_marker_value_or_last_run_time)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(RoundTrip_should_not_mutate_reported_existing_marker_value_or_last_run_time))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var serializer = new DataHubDataSerializer();
                    using var stream = CreateStream($$"""
                    {
                      "AgentId": "D365-AGENT",
                      "DataSource": "D365",
                      "EntityType": "contact",
                      "Value": "{{DateLikeMarkerValue}}",
                      "LastRunTime": "{{ReportedLastRunTimeValue}}",
                      "_dt": "MergeMarker",
                      "pk": "",
                      "id": "0dffba44-165f-4776-bbe0-500768e89979",
                      "_etag": "\"3d00cb36-0000-1a00-0000-6a1793bf0000\"",
                      "_ts": 1779930047
                    }
                    """);

                    var marker = serializer.FromStream<MergeMarker>(stream);
                    var document = SerializeToJObject(serializer, marker);

                    document.Value<string>("Value").Should().Be(DateLikeMarkerValue);
                    document.Value<string>("LastRunTime").Should().Be(ReportedLastRunTimeValue);
                    document.Value<string>("id").Should().Be("0dffba44-165f-4776-bbe0-500768e89979");
                    document.Value<string>("_dt").Should().Be(nameof(MergeMarker));
                    document.Value<string>("pk").Should().BeEmpty();
                    document.Value<long>("_ts").Should().Be(1779930047);

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
    public async Task ToStream_should_not_mutate_jobject_string_values_that_look_like_dates()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(ToStream_should_not_mutate_jobject_string_values_that_look_like_dates)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(ToStream_should_not_mutate_jobject_string_values_that_look_like_dates))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var serializer = new DataHubDataSerializer();
                    var document = new JObject
                    {
                        ["Value"] = DateLikeMarkerValue,
                        ["LastRunTime"] = ReportedLastRunTimeValue,
                        ["Nested"] = new JObject
                        {
                            ["Value"] = DateLikeMarkerValue
                        }
                    };

                    var serialized = SerializeToJObject(serializer, document);

                    serialized["Value"]!.Type.Should().Be(JTokenType.String);
                    serialized.Value<string>("Value").Should().Be(DateLikeMarkerValue);
                    serialized["LastRunTime"]!.Type.Should().Be(JTokenType.String);
                    serialized.Value<string>("LastRunTime").Should().Be(ReportedLastRunTimeValue);
                    serialized["Nested"]!["Value"]!.Type.Should().Be(JTokenType.String);
                    serialized["Nested"]!.Value<string>("Value").Should().Be(DateLikeMarkerValue);

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
    public async Task ToStream_should_omit_null_last_run_time_but_keep_date_like_value()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(ToStream_should_omit_null_last_run_time_but_keep_date_like_value)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(ToStream_should_omit_null_last_run_time_but_keep_date_like_value))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var serializer = new DataHubDataSerializer();
                    var marker = new MergeMarker
                    {
                        id = "merge-marker-1",
                        AgentId = "agent-1",
                        DataSource = "SRC1",
                        EntityType = "TypeA",
                        Value = DateLikeMarkerValue,
                        LastRunTime = null,
                        pk = ""
                    };

                    var document = SerializeToJObject(serializer, marker);

                    document.Value<string>("Value").Should().Be(DateLikeMarkerValue);
                    document.ContainsKey("LastRunTime").Should().BeFalse();

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
    public async Task RoundTrip_should_preserve_null_char_escaping_without_date_mutation()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(RoundTrip_should_preserve_null_char_escaping_without_date_mutation)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(RoundTrip_should_preserve_null_char_escaping_without_date_mutation))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var serializer = new DataHubDataSerializer();
                    var marker = new MergeMarker
                    {
                        id = "merge-marker-1",
                        AgentId = "agent-1",
                        DataSource = "SRC1",
                        EntityType = "TypeA",
                        Value = $"before\0{DateLikeMarkerValue}\0after",
                        LastRunTime = LastRunTime,
                        pk = ""
                    };

                    var serializedContent = SerializeToString(serializer, marker);
                    using var roundTripStream = CreateStream(serializedContent);
                    var roundTripped = serializer.FromStream<MergeMarker>(roundTripStream);

                    serializedContent.Should().Contain("\\u0000");
                    roundTripped.Value.Should().Be(marker.Value);
                    roundTripped.LastRunTime.Should().Be(LastRunTime);

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
    [InlineData("2026-05-28T11:00:21.8979541+10:00")]
    [InlineData("2026-05-28T01:00:21.8979541Z")]
    [InlineData("2026-05-27T20:30:21.8979541-04:30")]
    public async Task DateTimeOffsetValue_helpers_should_parse_string_tokens_without_requiring_date_tokens(string value)
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(DateTimeOffsetValue_helpers_should_parse_string_tokens_without_requiring_date_tokens)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(DateTimeOffsetValue_helpers_should_parse_string_tokens_without_requiring_date_tokens))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var token = new JObject
                    {
                        ["Timestamp"] = value
                    };

                    token["Timestamp"]!.Type.Should().Be(JTokenType.String);
                    token.DateTimeOffsetValueRequired("Timestamp").Should().Be(DateTimeOffset.Parse(value, CultureInfo.InvariantCulture));
                    token["Timestamp"]!.TryAsDateTimeOffset(out var parsed).Should().BeTrue();
                    parsed.Should().Be(DateTimeOffset.Parse(value, CultureInfo.InvariantCulture));

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
    public async Task DateTimeOffsetValue_helpers_should_handle_date_tokens_for_legacy_dynamic_values()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(DateTimeOffsetValue_helpers_should_handle_date_tokens_for_legacy_dynamic_values)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(DateTimeOffsetValue_helpers_should_handle_date_tokens_for_legacy_dynamic_values))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var timestamp = new DateTimeOffset(2026, 5, 28, 11, 0, 21, 897, TimeSpan.FromHours(10));
                    var token = new JObject
                    {
                        ["Timestamp"] = JToken.FromObject(timestamp)
                    };

                    token["Timestamp"]!.Type.Should().Be(JTokenType.Date);
                    token.DateTimeOffsetValueRequired("Timestamp").Should().Be(timestamp);
                    token["Timestamp"]!.TryAsDateTimeOffset(out var parsed).Should().BeTrue();
                    parsed.Should().Be(timestamp);

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
    public async Task DateTimeOffsetValue_helpers_should_treat_missing_null_and_empty_values_as_null_or_unparseable()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(DateTimeOffsetValue_helpers_should_treat_missing_null_and_empty_values_as_null_or_unparseable)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(DateTimeOffsetValue_helpers_should_treat_missing_null_and_empty_values_as_null_or_unparseable))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var token = new JObject
                    {
                        ["NullTimestamp"] = JValue.CreateNull(),
                        ["EmptyTimestamp"] = ""
                    };

                    token.DateTimeOffsetValue("MissingTimestamp").Should().BeNull();
                    token.DateTimeOffsetValue("NullTimestamp").Should().BeNull();
                    token.DateTimeOffsetValue("EmptyTimestamp").Should().BeNull();
                    token["EmptyTimestamp"]!.TryAsDateTimeOffset(out _).Should().BeFalse();
                    token["NotDate"] = "not-a-date";
                    token["NotDate"]!.TryAsDateTimeOffset(out _).Should().BeFalse();

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
    public async Task DateTimeOffsetValueRequired_should_throw_for_unparseable_string_values()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(DateTimeOffsetValueRequired_should_throw_for_unparseable_string_values)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(DateTimeOffsetValueRequired_should_throw_for_unparseable_string_values))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var token = new JObject
                    {
                        ["Timestamp"] = "not-a-date"
                    };

                    var act = () => token.DateTimeOffsetValueRequired("Timestamp");

                    act.Should().Throw<FormatException>();

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
    public async Task FromStream_should_deserialize_real_date_properties_when_source_token_is_still_a_string()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(FromStream_should_deserialize_real_date_properties_when_source_token_is_still_a_string)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(FromStream_should_deserialize_real_date_properties_when_source_token_is_still_a_string))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var serializer = new DataHubDataSerializer();
                    using var stream = CreateStream($$"""
                    {
                      "id": "sample-1",
                      "Timestamp": "{{ReportedLastRunTimeValue}}",
                      "NullableTimestamp": "{{DateLikeMarkerValue}}",
                      "StringTimestamp": "{{DateLikeMarkerValue}}"
                    }
                    """);

                    var sample = serializer.FromStream<DateHandlingSample>(stream);

                    sample.Timestamp.Should().Be(LastRunTime);
                    sample.NullableTimestamp.Should().Be(DateTimeOffset.Parse(DateLikeMarkerValue, CultureInfo.InvariantCulture));
                    sample.StringTimestamp.Should().Be(DateLikeMarkerValue);

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
    public async Task ToStream_should_write_real_date_properties_as_dates_and_string_date_values_as_strings()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(ToStream_should_write_real_date_properties_as_dates_and_string_date_values_as_strings)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(ToStream_should_write_real_date_properties_as_dates_and_string_date_values_as_strings))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var serializer = new DataHubDataSerializer();
                    var sample = new DateHandlingSample
                    {
                        id = "sample-1",
                        Timestamp = LastRunTime,
                        NullableTimestamp = DateTimeOffset.Parse(DateLikeMarkerValue, CultureInfo.InvariantCulture),
                        StringTimestamp = DateLikeMarkerValue
                    };

                    var document = SerializeToJObject(serializer, sample);

                    document.Value<string>("Timestamp").Should().Be(ReportedLastRunTimeValue);
                    DateTimeOffset.Parse(document.Value<string>("NullableTimestamp")!, CultureInfo.InvariantCulture).Should().Be(sample.NullableTimestamp);
                    document.Value<string>("StringTimestamp").Should().Be(DateLikeMarkerValue);

                    return await ActionResult(currentObject, stash);
                });
            })
            .Then("The assertions complete successfully", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .Run();
    }

    public static TheoryData<string> DateLikeStringValues()
    {
        return new TheoryData<string>
        {
            "2026-05-27T17:00:51.0000000+10:00",
            "2026-05-27T17:00:51+10:00",
            "2026-05-27T07:00:51Z",
            "2026-05-27T17:00:51.0000000-04:30",
            "2026-05-27",
            "05/27/2026 17:00:51 +10:00",
            "27/05/2026 17:00:51 +10:00"
        };
    }

    private static JObject SerializeToJObject<T>(DataHubDataSerializer serializer, T value)
    {
        var content = SerializeToString(serializer, value);
        using var jsonReader = new JsonTextReader(new StringReader(content))
        {
            DateParseHandling = DateParseHandling.None
        };

        return JObject.Load(jsonReader);
    }

    private static string SerializeToString<T>(DataHubDataSerializer serializer, T value)
    {
        using var stream = serializer.ToStream(value);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static MemoryStream CreateStream(string json)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(json));
    }

    private sealed class DateHandlingSample : DataDocument
    {
        public DateTimeOffset Timestamp { get; set; }
        public DateTimeOffset? NullableTimestamp { get; set; }
        public string StringTimestamp { get; set; } = string.Empty;
    }
}
