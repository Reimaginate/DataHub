using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.Helpers;
using Xunit;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class ChangeTrackingDateTokenTests
{
    public static TheoryData<string, bool, bool> DatePairs
    {
        get
        {
            var cases = new TheoryData<string, bool, bool>();
            foreach (var shape in new[] { "root", "property", "array", "nested" })
            foreach (var sameInstant in new[] { false, true })
            foreach (var reverse in new[] { false, true })
                cases.Add(shape, sameInstant, reverse);
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(DatePairs))]
    public void Date_token_comparison_should_use_instants_and_produce_replayable_changes(
        string shape, bool sameInstant, bool reverse)
    {
        var utc = new DateTime(2026, 9, 18, 0, 30, 0, DateTimeKind.Utc).AddTicks(1234567);
        var offset = sameInstant
            ? new DateTimeOffset(utc).ToOffset(TimeSpan.FromHours(11))
            : new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Unspecified), TimeSpan.FromHours(11));
        var left = Wrap(shape, reverse ? new JValue(offset) : new JValue(utc));
        var right = Wrap(shape, reverse ? new JValue(utc) : new JValue(offset));
        var leftSnapshot = left.ToString(Formatting.None);
        var rightSnapshot = right.ToString(Formatting.None);

        var diff = ChangeTrackingHelper.DiffIgnoringEquivalentValueTokens(left, right);

        left.ToString(Formatting.None).Should().Be(leftSnapshot);
        right.ToString(Formatting.None).Should().Be(rightSnapshot);
        if (sameInstant)
        {
            diff.Should().BeNull("an offset representation change alone is not a different instant");
        }
        else
        {
            diff.Should().NotBeNull("equal clock ticks with different offsets represent different instants");
            var replayed = ChangeTrackingHelper.JsonDiffPatch.Patch(left.DeepClone(), diff);
            var resultToken = shape switch
            {
                "root" => replayed,
                "property" => replayed["Value"]!,
                "array" => replayed[0]!,
                _ => replayed.SelectToken("Events[0].Value")!
            };
            resultToken.TryAsDateTimeOffset(out var actual).Should().BeTrue();
            var expected = reverse ? new DateTimeOffset(utc) : offset;
            actual.UtcTicks.Should().Be(expected.UtcTicks);
            actual.Offset.Should().Be(expected.Offset);
        }
    }

    [Fact]
    public void Array_change_should_preserve_offset_of_unchanged_date_elements_when_replayed()
    {
        var date = new DateTimeOffset(2026, 9, 18, 0, 30, 0, TimeSpan.FromHours(11)).AddTicks(1234567);
        var left = new JArray(new JValue(date), "old");
        var right = new JArray(new JValue(date), "new");

        var diff = ChangeTrackingHelper.DiffIgnoringEquivalentValueTokens(left, right);
        var replayed = ChangeTrackingHelper.JsonDiffPatch.Patch(left.DeepClone(), diff);

        replayed[0]!.TryAsDateTimeOffset(out var actual).Should().BeTrue();
        actual.UtcTicks.Should().Be(date.UtcTicks);
        actual.Offset.Should().Be(date.Offset);
        replayed[1]!.Value<string>().Should().Be("new");
    }

    private static JToken Wrap(string shape, JToken value) => shape switch
    {
        "root" => value,
        "property" => new JObject { ["Value"] = value },
        "array" => new JArray(value),
        "nested" => new JObject { ["Events"] = new JArray(new JObject { ["Value"] = value }) },
        _ => throw new ArgumentOutOfRangeException(nameof(shape))
    };
}
