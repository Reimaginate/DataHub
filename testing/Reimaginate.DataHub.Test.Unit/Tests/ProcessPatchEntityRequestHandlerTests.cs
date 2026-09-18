using System.Globalization;
using System.Text;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Requests.External.CLI.DeserializeCliRequest;
using Reimaginate.DataHub.Requests.External.Client.DeserializeClientRequest;
using Reimaginate.DataHub.Requests.Internal.ProcessPatchEntity;
using Reimaginate.DataHub.Services.TimeService;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;
using Reimaginate.ProcessingLockService;
using Reimaginate.ProcessingLockService.Abstractions;
using Xunit;
using ClientPatchRequest = Reimaginate.DataHub.SharedModels.Requests.Client.PatchEntityRequest;
using CliPatchRequest = Reimaginate.DataHub.SharedModels.Requests.CLI.PatchEntityRequest;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class ProcessPatchEntityRequestHandlerTests
{
    private static readonly string[] ValueCases =
    [
        "datetime-utc", "datetime-local", "datetime-unspecified",
        "offset-utc", "offset-positive", "offset-negative", "offset-fractional",
        "json-null", "clr-null", "string", "empty-string", "date-only-string",
        "timestamp-string", "integer", "decimal", "boolean", "object", "array"
    ];

    public static TheoryData<string, string> ReplacementCases
    {
        get
        {
            var cases = new TheoryData<string, string>();
            foreach (var existing in new[] { "string", "datetime-utc", "offset-positive", "json-null" })
            foreach (var replacement in ValueCases)
                cases.Add(existing, replacement);
            return cases;
        }
    }

    public static TheoryData<string, string> AdditionCases
    {
        get
        {
            var cases = new TheoryData<string, string>();
            foreach (var operation in new[] { "add", "set" })
            foreach (var replacement in ValueCases)
                cases.Add(operation, replacement);
            return cases;
        }
    }

    public static TheoryData<string, string> ReferenceCases
    {
        get
        {
            var cases = new TheoryData<string, string>();
            foreach (var destination in new[] { "existing-string", "missing-set", "missing-add" })
            foreach (var source in new[] { "string", "datetime-utc", "offset-positive", "json-null", "integer", "object", "array", "missing" })
                cases.Add(destination, source);
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(ReplacementCases))]
    public async Task Set_should_preserve_replacement_value_and_type(string existingCase, string replacementCase)
    {
        var entity = Entity();
        entity["Value"] = existingCase switch
        {
            "string" => new JValue("2020-01-01T00:00:00Z"),
            "datetime-utc" => new JValue(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            "offset-positive" => new JValue(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.FromHours(11))),
            _ => Value(existingCase)
        };
        var replacement = Value(replacementCase);

        var response = await Apply(entity, new Patch { Operation = "set", Path = "Value", Value = replacement! });

        AssertSuccessful(response);
        AssertValue(response.UpdatedEntity["Value"], replacement);
    }

    [Theory]
    [MemberData(nameof(AdditionCases))]
    public async Task Missing_property_should_accept_typed_values_and_null(string operation, string replacementCase)
    {
        var replacement = Value(replacementCase);

        var response = await Apply(Entity(), new Patch { Operation = operation, Path = "Details.Value", Value = replacement! });

        AssertSuccessful(response);
        AssertValue(response.UpdatedEntity.SelectToken("Details.Value"), replacement);
        response.ChangeSet.Should().NotBeNull();
    }

    [Theory]
    [InlineData("set", "datetime-utc")]
    [InlineData("set", "offset-positive")]
    [InlineData("set", "json-null")]
    [InlineData("set", "clr-null")]
    [InlineData("add", "datetime-unspecified")]
    [InlineData("add", "offset-negative")]
    [InlineData("add", "json-null")]
    [InlineData("add", "clr-null")]
    public async Task Array_elements_should_accept_dates_and_null(string operation, string replacementCase)
    {
        var entity = Entity();
        entity["Values"] = new JArray("old");
        var path = operation == "set" ? "Values[0]" : "Values[1]";
        var replacement = Value(replacementCase);

        var response = await Apply(entity, new Patch { Operation = operation, Path = path, Value = replacement! });

        AssertSuccessful(response);
        AssertValue(response.UpdatedEntity.SelectToken(path), replacement);
        ((JArray)response.UpdatedEntity["Values"]!).Count.Should().Be(operation == "set" ? 1 : 2);
        if (operation == "add")
            response.UpdatedEntity.SelectToken("Values[0]")!.Value<string>().Should().Be("old");
    }

    [Theory]
    [MemberData(nameof(ReferenceCases))]
    public async Task Property_reference_should_copy_original_typed_value_or_null(string destination, string sourceCase)
    {
        var entity = Entity();
        var expected = sourceCase == "missing" ? null : Value(sourceCase);
        entity["Source"] = new JObject();
        if (sourceCase != "missing")
            entity["Source"]!["Value"] = expected;
        if (destination == "existing-string")
            entity["Target"] = "old";

        var response = await Apply(entity, new Patch
        {
            Operation = destination == "missing-add" ? "add" : "set",
            Path = "Target",
            Value = "^.Source.Value"
        });

        AssertSuccessful(response);
        AssertValue(response.UpdatedEntity["Target"], expected);
        if (expected is JContainer)
        {
            response.UpdatedEntity["Target"].Should().NotBeSameAs(entity.SelectToken("Source.Value"));
            ((JContainer)response.UpdatedEntity["Target"]!).RemoveAll();
            AssertValue(response.UpdatedEntity.SelectToken("Source.Value"), expected);
        }
    }

    [Fact]
    public async Task Property_reference_should_read_snapshot_before_earlier_operations()
    {
        var entity = Entity();
        var originalDate = Value("offset-positive")!;
        entity["Source"] = new JArray(originalDate);
        entity["Target"] = "old";

        var response = await Apply(entity,
            new Patch { Operation = "set", Path = "Source[0]", Value = Value("offset-negative")! },
            new Patch { Operation = "set", Path = "Target", Value = "^.Source[0]" });

        AssertSuccessful(response);
        AssertValue(response.UpdatedEntity["Target"], originalDate);
        AssertValue(response.UpdatedEntity.SelectToken("Source[0]"), Value("offset-negative"));
    }

    [Theory]
    [InlineData("^literal")]
    [InlineData("prefix ^.Source")]
    public async Task Strings_without_reference_prefix_should_remain_literal(string replacement)
    {
        var entity = Entity();
        entity["Value"] = "old";

        var response = await Apply(entity, new Patch { Operation = "set", Path = "Value", Value = replacement });

        AssertSuccessful(response);
        AssertValue(response.UpdatedEntity["Value"], new JValue(replacement));
    }

    [Theory]
    [InlineData("abc-123", "([a-z]+)-(\\d+)", "$2:$1", "123:abc")]
    [InlineData("abc-123", "\\d+", "", "abc-")]
    [InlineData("abc", "^abc$", "^.Source", "^.Source")]
    [InlineData("abc", "\\d+", "replacement", "abc")]
    public async Task Regex_should_preserve_captures_empty_replacement_and_precedence(
        string existing, string regex, string replacement, string expected)
    {
        var entity = Entity();
        entity["Value"] = existing;
        entity["Source"] = "must not be copied";

        var response = await Apply(entity, new Patch { Operation = "set", Path = "Value", Regex = regex, Value = replacement });

        AssertSuccessful(response);
        AssertValue(response.UpdatedEntity["Value"], new JValue(expected));
    }

    [Theory]
    [InlineData("client", "\"2026-09-18T00:30:00.1234567+11:00\"")]
    [InlineData("cli", "\"2026-09-18T00:30:00.1234567+11:00\"")]
    [InlineData("client", "null")]
    [InlineData("cli", "null")]
    public async Task Deserialized_patch_should_replace_timestamp_read_from_storage(string endpoint, string jsonValue)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("""
            {"id":"entity-1","entityType":"Event","lastUpdated":"2020-01-01T00:00:00Z","Value":"2020-01-01T00:00:00Z"}
            """));
        var entity = new DataHubDataSerializer().FromStream<JObject>(stream);
        entity["Value"]!.Type.Should().Be(JTokenType.String);
        var envelope = new SerializedRequest
        {
            RequestType = "PatchEntityRequest",
            Data = $$"""{"Operations":[{"Operation":"set","Path":"Value","Value":{{jsonValue}}}]}"""
        };
        List<Patch> operations;
        if (endpoint == "client")
        {
            var request = await new DeserializeClientRequestRequestHandler().HandleAsync(
                new DeserializeClientRequestRequest { SerializedRequest = envelope }, CancellationToken.None);
            operations = ((ClientPatchRequest)request).Operations;
        }
        else
        {
            var request = await new DeserializeCliRequestRequestHandler().HandleAsync(
                new DeserializeCliRequestRequest { SerializedRequest = envelope }, CancellationToken.None);
            operations = ((CliPatchRequest)request).Operations;
        }
        var expected = jsonValue == "null" ? JValue.CreateNull() : Value("offset-positive");
        AssertValue(operations.Single().Value, expected);

        var response = await Apply(entity, operations.ToArray());

        AssertSuccessful(response);
        AssertValue(response.UpdatedEntity["Value"], expected);
        response.ChangeSet.Should().NotBeNull();
    }

    [Fact]
    public async Task Equivalent_timestamp_replacement_should_not_create_a_change_set()
    {
        var entity = Entity();
        entity["Value"] = "2026-09-17T13:30:00.1234567Z";
        var replacement = Value("offset-positive");

        var response = await Apply(entity, new Patch { Operation = "set", Path = "Value", Value = replacement! });

        AssertSuccessful(response);
        AssertValue(response.UpdatedEntity["Value"], replacement);
        response.ChangeSet.Should().BeNull("the two timestamps describe exactly the same instant");
    }

    [Theory]
    [InlineData("offset-positive")]
    [InlineData("offset-fractional")]
    public async Task Same_clock_time_with_different_offset_should_replace_the_instant(string replacementCase)
    {
        // Unlike the string-conversion regression, this exercises an existing date token.
        // Equal clock ticks do not imply equal instants when the offset changes.
        var entity = Entity();
        entity["Value"] = Value("datetime-utc");
        var replacement = Value(replacementCase);
        ((JValue)entity["Value"]!).Value.Should().BeOfType<DateTime>().Subject.Ticks
            .Should().NotBe(((DateTimeOffset)((JValue)replacement!).Value!).UtcTicks);

        var response = await Apply(entity, new Patch { Operation = "set", Path = "Value", Value = replacement! });

        AssertSuccessful(response);
        AssertValue(response.UpdatedEntity["Value"], replacement);
        response.ChangeSet.Should().NotBeNull("a different instant is a real update");
    }

    [Theory]
    [InlineData("regex")]
    [InlineData("reference")]
    public async Task Invalid_expression_should_report_failure_and_continue_with_later_operations(string expression)
    {
        var entity = Entity();
        entity["Value"] = "unchanged";
        var invalidPatch = new Patch
        {
            Operation = "set",
            Path = "Value",
            Regex = expression == "regex" ? "[" : null,
            Value = expression == "reference" ? "^.Source[" : "replacement"
        };

        var response = await Apply(entity, invalidPatch,
            new Patch { Operation = "set", Path = "After", Value = "applied" });

        response.Success.Should().BeFalse();
        var failure = response.PatchFailures.Should().ContainSingle().Subject;
        failure.Patch.Should().BeSameAs(invalidPatch);
        failure.FailureReason.Should().NotBeNullOrWhiteSpace();
        AssertValue(response.UpdatedEntity["Value"], new JValue("unchanged"));
        AssertValue(response.UpdatedEntity["After"], new JValue("applied"));
    }

    [Fact]
    public async Task Failed_operation_should_be_reported_while_other_operations_still_apply()
    {
        var entity = Entity();
        entity["Before"] = "old";
        entity["After"] = "old";
        var failedPatch = new Patch { Operation = "add", Path = "Before", Value = "duplicate" };

        var response = await Apply(entity,
            new Patch { Operation = "set", Path = "Before", Value = "first" },
            failedPatch,
            new Patch { Operation = "set", Path = "After", Value = "last" });

        response.Success.Should().BeFalse();
        var failure = response.PatchFailures.Should().ContainSingle().Subject;
        failure.Patch.Should().BeSameAs(failedPatch);
        failure.FailureReason.Should().Be("Path already exists");
        AssertValue(response.UpdatedEntity["Before"], new JValue("first"));
        AssertValue(response.UpdatedEntity["After"], new JValue("last"));
        response.ChangeSet.Should().NotBeNull();
    }

    private static JObject Entity() => new()
    {
        ["id"] = "entity-1",
        ["entityType"] = "Event",
        ["lastUpdated"] = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero)
    };

    private static JToken? Value(string name) => name switch
    {
        "datetime-utc" => new JValue(new DateTime(2026, 9, 18, 0, 30, 0, DateTimeKind.Utc).AddTicks(1234567)),
        "datetime-local" => new JValue(new DateTime(2026, 9, 18, 0, 30, 0, DateTimeKind.Local).AddTicks(1234567)),
        "datetime-unspecified" => new JValue(new DateTime(2026, 9, 18, 0, 0, 0, DateTimeKind.Unspecified)),
        "offset-utc" => new JValue(DateTimeOffset.Parse("2026-09-18T00:30:00.1234567Z", CultureInfo.InvariantCulture)),
        "offset-positive" => new JValue(DateTimeOffset.Parse("2026-09-18T00:30:00.1234567+11:00", CultureInfo.InvariantCulture)),
        "offset-negative" => new JValue(DateTimeOffset.Parse("2026-09-18T23:30:00.1234567-07:00", CultureInfo.InvariantCulture)),
        "offset-fractional" => new JValue(DateTimeOffset.Parse("2026-09-18T00:30:00.1234567+05:45", CultureInfo.InvariantCulture)),
        "json-null" => JValue.CreateNull(),
        "clr-null" => null,
        "string" => new JValue("replacement"),
        "empty-string" => new JValue(""),
        "date-only-string" => new JValue("2026-09-18"),
        "timestamp-string" => new JValue("2026-09-18T00:30:00.1234567+11:00"),
        "integer" => new JValue(42L),
        "decimal" => new JValue(12.345m),
        "boolean" => new JValue(false),
        "object" => new JObject { ["Name"] = "value", ["Optional"] = JValue.CreateNull() },
        "array" => new JArray(1, "two", JValue.CreateNull()),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown test value")
    };

    private static void AssertSuccessful(ProcessPatchEntityResponse response)
    {
        response.Success.Should().BeTrue("the patch should succeed; failures: {0}; overall: {1}",
            string.Join("; ", response.PatchFailures?.Select(f => f.FailureReason) ?? []), response.FailureReason);
        response.PatchFailures.Should().BeEmpty();
    }

    private static void AssertValue(JToken? actual, JToken? expected)
    {
        expected ??= JValue.CreateNull();
        actual.Should().NotBeNull("the property/array element must exist, including when its value is JSON null");
        actual!.Type.Should().Be(expected.Type);
        JToken.DeepEquals(actual, expected).Should().BeTrue("the replacement must retain its value");
        if (expected is JValue { Value: DateTimeOffset offset })
        {
            var result = ((JValue)actual).Value.Should().BeOfType<DateTimeOffset>().Subject;
            result.Ticks.Should().Be(offset.Ticks);
            result.Offset.Should().Be(offset.Offset);
            result.UtcTicks.Should().Be(offset.UtcTicks);
        }
        else if (expected is JValue { Value: DateTime date })
        {
            var result = ((JValue)actual).Value.Should().BeOfType<DateTime>().Subject;
            result.Ticks.Should().Be(date.Ticks);
            result.Kind.Should().Be(date.Kind);
        }
    }

    // Exercise the public handler without persistence or live services. Keep the original
    // cached entity intact, including when an individual patch fails.
    private static async Task<ProcessPatchEntityResponse> Apply(JObject entity, params Patch[] operations)
    {
        var originalSnapshot = entity.DeepClone();
        var mediator = Substitute.For<IMediator>();
        var lockService = Substitute.For<IProcessingLockService>();
        var processingLock = new ProcessingLock();
        lockService.WaitForLockAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(),
                Arg.Any<TimeSpan?>(), Arg.Any<TimeSpan?>())
            .Returns(Task.FromResult(new Response<ProcessingLock>(true, processingLock, null)));
        lockService.ReleaseLockAsync(processingLock, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new Response<Null>(true, new Null(), null)));
        var handler = new ProcessPatchEntityRequestHandler(mediator, lockService, Substitute.For<ITimeService>());

        var response = await handler.HandleAsync(new ProcessPatchEntityRequest
        {
            RequestId = "request-1",
            CorrelationId = "correlation-1",
            DataSource = DataSources.DataHub,
            EntityType = "Event",
            EntityId = "entity-1",
            Operations = operations.ToList(),
            Cache = new Dictionary<string, object> { ["DataHubEntities"] = new List<JObject> { entity } },
            CommitToDb = false,
            Silent = true
        }, CancellationToken.None);

        JToken.DeepEquals(entity, originalSnapshot).Should().BeTrue("patching must not mutate the cached original entity");
        mediator.ReceivedCalls().Should().BeEmpty("cached dry runs should not access live services");
        await lockService.Received(1).ReleaseLockAsync(processingLock, CancellationToken.None);
        return response;
    }
}
