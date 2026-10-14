using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.SharedModels.Constants;
using Xunit;

namespace Reimaginate.DataHub.Test.Unit.Tests;

[Trait("Category", "MergeRegression")]
public class AlternateKeyNullMergeRegressionTests
{
    [Theory]
    [InlineData(null)] // Missing property.
    [InlineData("null")] // JSON null is a JValue, not a missing token.
    [InlineData("[]")]
    [InlineData("[{\"Key\":\"customer.account\",\"Value\":\"account-1\"}]")]
    public async Task Initial_import_then_changed_source_preserves_identity_and_records_changes(string? alternateKeysJson)
    {
        using var db = new MergeRegressionHarness(false);
        var initial = MergeRegressionHarness.Booked();
        SetAlternateKeys(initial.Data, alternateKeysJson);

        var imported = await db.MergePublicAsync(true, [initial]);

        Assert.True(imported.Success, imported.FailureReason);
        var created = Assert.Single(imported.Results);
        Assert.Equal(MergeOutcomes.NewEntityCreated, created.MergeOutcome);
        var entityId = created.DataHubEntityId;
        Assert.Equal(42m, db.Stored[entityId].Value<decimal>("Total"));

        // Reproduce the report with only a business field and the source timestamp changed.
        var changed = MergeRegressionHarness.Booked();
        changed.Data = (JObject)initial.Data.DeepClone();
        changed.Data["Total"] = 0m;
        changed.Data["lastUpdated"] = MergeRegressionHarness.SourceTime(2);
        var updated = await db.MergePublicAsync(true, [changed]);

        Assert.True(updated.Success, updated.FailureReason);
        var result = Assert.Single(updated.Results);
        Assert.Equal(MergeOutcomes.EntityMatchedAndUpdated, result.MergeOutcome);
        Assert.Equal(entityId, result.DataHubEntityId);
        var entity = Assert.Single(db.Stored).Value;
        Assert.Equal(entityId, entity.Value<string>("id"));
        Assert.Equal(0m, entity.Value<decimal>("Total"));
        AssertKey(entity, "testsource.reservation", "booking-1");
        if (alternateKeysJson?.StartsWith("[{") == true)
            AssertKey(entity, "customer.account", "account-1");

        foreach (var history in new[]
                 {
                     db.HistoryFor("TestSource", "Reservation", "booking-1"),
                     db.HistoryFor(DataSources.DataHub, "Booking", entityId)
                 })
        {
            Assert.Single(history, e => e.EntryType == ChangeTrackingEntryTypes.Init);
            var update = Assert.Single(history, e => e.EntryType == ChangeTrackingEntryTypes.Update);
            Assert.Equal(MergeRegressionHarness.SourceTime(2), update.Timestamp);
            Assert.Equal(42m, ChangeTrackingHelper.ReassembleEntity(history.Where(e => e.Timestamp < update.Timestamp).ToList()).Value<decimal>("Total"));
            Assert.Equal(0m, ChangeTrackingHelper.ReassembleEntity(history).Value<decimal>("Total"));
        }
    }

    [Theory]
    [InlineData(null, "account-1")]
    [InlineData("null", "account-1")]
    [InlineData("[]", "account-1")]
    [InlineData("[{\"Key\":\"customer.account\",\"Value\":\"account-2\"},{\"Key\":\"customer.new\",\"Value\":\"new-key\"}]", "account-2")]
    public async Task Source_keys_merge_without_removing_existing_destination_keys(string? alternateKeysJson, string expectedAccount)
    {
        using var db = new MergeRegressionHarness(false);
        var initial = MergeRegressionHarness.Booked();
        SetAlternateKeys(initial.Data, "[{\"Key\":\"customer.account\",\"Value\":\"account-1\"},{\"Key\":\"customer.retained\",\"Value\":\"retained-key\"}]");
        var imported = await db.MergePublicAsync(true, [initial]);
        Assert.Equal(MergeOutcomes.NewEntityCreated, Assert.Single(imported.Results).MergeOutcome);
        var entityId = imported.Results.Single().DataHubEntityId;
        var changed = MergeRegressionHarness.Cancelled();
        SetAlternateKeys(changed.Data, alternateKeysJson);

        var updated = await db.MergePublicAsync(true, [changed]);

        Assert.Equal(MergeOutcomes.EntityMatchedAndUpdated, Assert.Single(updated.Results).MergeOutcome);
        var entity = Assert.Single(db.Stored).Value;
        Assert.Equal(entityId, entity.Value<string>("id"));
        AssertKey(entity, "testsource.reservation", "booking-1");
        AssertKey(entity, "customer.account", expectedAccount);
        AssertKey(entity, "customer.retained", "retained-key");
        if (expectedAccount == "account-2") AssertKey(entity, "customer.new", "new-key");
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("\"invalid\"")]
    [InlineData("42")]
    [InlineData("true")]
    public async Task Changed_source_with_non_array_keys_is_rejected_without_changing_entity_or_history(string alternateKeysJson)
    {
        using var db = new MergeRegressionHarness(false);
        var imported = await db.MergePublicAsync(true, [MergeRegressionHarness.Booked()]);
        Assert.Equal(MergeOutcomes.NewEntityCreated, Assert.Single(imported.Results).MergeOutcome);
        var entityId = imported.Results.Single().DataHubEntityId;
        var baseline = db.Stored[entityId].DeepClone();
        var changed = MergeRegressionHarness.Cancelled();
        SetAlternateKeys(changed.Data, alternateKeysJson);

        var updated = await db.MergePublicAsync(true, [changed]);

        var failure = Assert.Single(updated.Results);
        Assert.Equal(MergeOutcomes.MergeFailed, failure.MergeOutcome);
        Assert.Contains("alternateKeys must be an array", failure.FailureReason);
        Assert.True(JToken.DeepEquals(baseline, Assert.Single(db.Stored).Value));
        Assert.Single(db.HistoryFor("TestSource", "Reservation", "booking-1"));
        Assert.Single(db.HistoryFor(DataSources.DataHub, "Booking", entityId));
        Assert.Equal(1, db.EntityWriteCount(entityId));
    }

    private static void SetAlternateKeys(JObject entity, string? json)
    {
        if (json == null) entity.Remove("alternateKeys");
        else entity["alternateKeys"] = JToken.Parse(json);
    }

    private static void AssertKey(JObject entity, string key, string value) =>
        Assert.Equal(value, Assert.Single(entity["alternateKeys"]!, k => k.Value<string>("Key") == key).Value<string>("Value"));
}
