using FluentAssertions;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.CLI.Tools.Helpers;
using Reimaginate.DataHub.SharedModels.Core;
using Xunit;

namespace Reimaginate.DataHub.CLI.Test.Unit;

public class CliHelpersTests
{
    [Fact]
    public void ParseDisplayProps_removes_duplicate_additional_properties()
    {
        var props = CliHelpers.ParseDisplayProps(
            "id,entityType,lastUpdated,alternateKeys",
            additionalProps: "FirstName,LastName,lastUpdated");

        props.Should().Equal("id", "entityType", "lastUpdated", "alternateKeys", "FirstName", "LastName");
    }

    [Fact]
    public void SummarizeResults_summarizes_alternate_keys_as_key_value_pairs()
    {
        var results = new List<JObject>
        {
            new()
            {
                [nameof(DataHubEntity.alternateKeys)] = new JArray
                {
                    new JObject
                    {
                        [nameof(AlternateKey.Key)] = "src.contact",
                        [nameof(AlternateKey.Value)] = "123"
                    },
                    new JObject
                    {
                        [nameof(AlternateKey.Key)] = "crm.account",
                        [nameof(AlternateKey.Value)] = "456"
                    }
                }
            }
        };

        var summarized = CliHelpers.SummarizeResults(false, results);

        summarized.Single()[nameof(DataHubEntity.alternateKeys)]!.Value<string>().Should().Be("src.contact=123, crm.account=456");
    }

    [Fact]
    public void SummarizeResults_handles_missing_null_and_empty_alternate_keys()
    {
        var results = new List<JObject>
        {
            new(),
            new()
            {
                [nameof(DataHubEntity.alternateKeys)] = JValue.CreateNull()
            },
            new()
            {
                [nameof(DataHubEntity.alternateKeys)] = new JArray()
            }
        };

        var summarized = CliHelpers.SummarizeResults(false, results);

        summarized[0].ContainsKey(nameof(DataHubEntity.alternateKeys)).Should().BeFalse();
        summarized[1][nameof(DataHubEntity.alternateKeys)]!.Type.Should().Be(JTokenType.Null);
        summarized[2][nameof(DataHubEntity.alternateKeys)]!.Value<string>().Should().BeEmpty();
    }

    [Fact]
    public void SummarizeResults_keeps_existing_last_updated_summary()
    {
        var results = new List<JObject>
        {
            new()
            {
                [nameof(DataHubEntity.lastUpdated)] = new DateTime(2026, 6, 6, 13, 14, 15)
            }
        };

        var summarized = CliHelpers.SummarizeResults(false, results);

        summarized.Single()[nameof(DataHubEntity.lastUpdated)]!.Value<string>().Should().Be("2026-06-06 01:14:15");
    }

    [Fact]
    public void SummarizeResults_preserves_raw_alternate_keys_when_expanded()
    {
        var alternateKeys = new JArray
        {
            new JObject
            {
                [nameof(AlternateKey.Key)] = "src.contact",
                [nameof(AlternateKey.Value)] = "123"
            }
        };
        var results = new List<JObject>
        {
            new()
            {
                [nameof(DataHubEntity.alternateKeys)] = alternateKeys
            }
        };

        var summarized = CliHelpers.SummarizeResults(true, results);

        summarized.Single()[nameof(DataHubEntity.alternateKeys)].Should().BeSameAs(alternateKeys);
    }
}
