using FluentAssertions;
using Newtonsoft.Json.Linq;
using NSubstitute;
using OneOf;
using Reimaginate.DataHub.DataAccess.Commands.UpsertDataHubEntities;
using Reimaginate.DataHub.DataAccess.Queries.GetTrackingEntries;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Requests.Internal.AddTrackedEntityChangeSets;
using Reimaginate.DataHub.Requests.Internal.CheckPreMergeRules;
using Reimaginate.DataHub.Requests.Internal.MaterializeDataHubEntity;
using Reimaginate.DataHub.Requests.Internal.ProcessNewEntityIntoExistingEntity;
using Reimaginate.DataHub.Requests.Internal.ProcessUpdatedEntities;
using Reimaginate.DataHub.Requests.Internal.ProcessUpdatedUntrackedEntities;
using Reimaginate.DataHub.Services.IdService;
using Reimaginate.DataHub.Services.TimeService;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.DataHub.SharedModels.Rules;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;
using Reimaginate.ProcessingLockService;
using Reimaginate.ProcessingLockService.Abstractions;
using Xunit;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class ParentMergeRuleHandlerTests
{
    private static readonly DateTimeOffset Timestamp = DateTimeOffset.Parse("2026-09-01T00:00:00Z");
    private const string Allow = PropertyMergeRuleActions.AlwaysOverwrite;
    private const string Deny = PropertyMergeRuleActions.NeverOverwrite;

    public static TheoryData<string, bool> MergePaths => new()
    {
        { "tracked", true }, { "tracked", false },
        { "untracked", true }, { "untracked", false },
        { "first", true }, { "first", false }
    };

    [Theory]
    [MemberData(nameof(MergePaths))]
    public async Task Parent_rules_and_explicit_child_overrides_control_descendants(string path, bool parentAllows)
    {
        var before = JObject.Parse("""{"Venue":{"EntityId":"old","Nested":{"Value":"old"},"Override":"old"},"VenueOther":{"EntityId":"old"},"Description":"old"}""");
        var after = JObject.Parse(before.ToString().Replace("old", "new"));
        var result = await Merge(path, before, after,
        [
            Rule("*", parentAllows ? Deny : Allow), // Deliberately first.
            Rule("Venue", parentAllows ? Allow : Deny),
            Rule("Venue.Override", parentAllows ? Deny : Allow)
        ]);

        result.Entity.SelectToken("Venue.EntityId")!.Value<string>().Should().Be(parentAllows ? "new" : "old");
        result.Entity.SelectToken("Venue.Nested.Value")!.Value<string>().Should().Be(parentAllows ? "new" : "old");
        result.Entity.SelectToken("Venue.Override")!.Value<string>().Should().Be(parentAllows ? "old" : "new");
        result.Entity.SelectToken("VenueOther.EntityId")!.Value<string>().Should().Be(parentAllows ? "old" : "new");
        result.Entity.Value<string>("Description").Should().Be(parentAllows ? "old" : "new");
        if (path == "tracked")
        {
            result.Changes.Should().NotBeNull();
            result.Changes!.SelectToken(parentAllows ? "Venue.EntityId" : "Venue.Override").Should().NotBeNull();
            result.Changes.SelectToken(parentAllows ? "Venue.Override" : "Venue.EntityId").Should().BeNull();
        }
    }

    public static IEnumerable<object[]> ValueCases()
    {
        var actions = new[] { Allow, Deny, PropertyMergeRuleActions.DoNotUpdate,
            PropertyMergeRuleActions.OverwriteIfNewer, PropertyMergeRuleActions.OverwriteIfEmpty,
            PropertyMergeRuleActions.OverwriteIfNotEmpty };
        var values = new (string Before, string After)[]
        {
            ("{}", "{\"Profile\":{\"Name\":\"new\"}}"), // Whole-object addition.
            ("{\"Profile\":{\"Name\":\"old\"}}", "{}"), // Whole-object removal.
            ("{\"Profile\":{\"Name\":\"old\"}}", "{\"Profile\":null}"),
            ("{\"Profile\":null}", "{\"Profile\":{\"Name\":\"new\"}}"),
            ("{\"Profile\":{\"Name\":\"old\"}}", "{\"Profile\":{}}"),
            ("{\"Profile\":{}}", "{\"Profile\":{\"Name\":\"new\"}}"),
            ("{\"Profile\":{\"Name\":null}}", "{\"Profile\":{\"Name\":\"new\"}}"),
            ("{\"Profile\":{\"Name\":\"old\"}}", "{\"Profile\":{\"Name\":null}}"),
            ("{\"Profile\":{\"Name\":\"\",\"Count\":0,\"Active\":false}}", "{\"Profile\":{\"Name\":\"new\",\"Count\":1,\"Active\":true}}"),
            ("{\"Profile\":{\"Name\":\"old\"}}", "{\"Profile\":{\"Name\":\"new\"}}"),
            ("{\"Profile\":{\"Tags\":[\"old\"]}}", "{\"Profile\":{\"Tags\":[\"new\",\"again\"]}}"),
            ("{\"Profile\":{\"Tags\":[\"old\"]}}", "{\"Profile\":{\"Tags\":[]}}")
        };
        foreach (var path in new[] { "tracked", "untracked", "first" })
        foreach (var action in actions)
        foreach (var value in values)
        foreach (var minutes in action == PropertyMergeRuleActions.OverwriteIfNewer ? new[] { -1, 0, 1 } : new[] { 1 })
            yield return [path, action, value.Before, value.After, minutes];
    }

    [Theory]
    [MemberData(nameof(ValueCases))]
    public async Task Inheritance_preserves_explicit_rule_action_semantics(string path, string action, string beforeJson, string afterJson, int minutes)
    {
        var before = JObject.Parse(beforeJson);
        var after = JObject.Parse(afterJson);
        // Use the actual diff representation for tracked updates, including array deltas.
        var payload = path == "tracked"
            ? (JObject?)ChangeTrackingHelper.DiffIgnoringEquivalentValueTokens(before, after) ?? new JObject()
            : after;
        var explicitRules = payload.GetAllProperties().Keys.Select(p => Rule(p, action)).ToList();
        explicitRules.Add(Rule("*", Deny));
        var explicitResult = await Merge(path, before, after, explicitRules, minutes);
        var inheritedResult = await Merge(path, before, after, [Rule("Profile", action), Rule("*", Deny)], minutes);

        JToken.DeepEquals(inheritedResult.Entity, explicitResult.Entity).Should().BeTrue("inheriting an action must preserve its existing semantics");
        JToken.DeepEquals(inheritedResult.Changes, explicitResult.Changes).Should().BeTrue();
    }

    [Theory]
    [InlineData("tracked", "new")]
    [InlineData("untracked", "new")]
    [InlineData("first", "old")]
    public async Task No_matching_rule_preserves_each_handlers_existing_default(string path, string expected)
    {
        var result = await Merge(path, JObject.Parse("""{"Value":"old"}"""), JObject.Parse("""{"Value":"new"}"""), [Rule("Other", Deny)]);
        result.Entity.Value<string>("Value").Should().Be(expected);
    }

    private static PropertyMergeRule Rule(string path, string action) => new() { PropertyName = path, Action = action };

    private static async Task<(JObject Entity, JObject? Changes)> Merge(string path, JObject before, JObject after,
        List<PropertyMergeRule> rules, int minutes = 1)
    {
        var target = (JObject)before.DeepClone();
        target["id"] = "entity-1";
        target["entityType"] = "Season";
        target["createdOn"] = Timestamp.AddDays(-1);
        target["lastUpdated"] = Timestamp;
        target["alternateKeys"] = JArray.FromObject(new[] { new AlternateKey("src.template", "source-1") });
        var incoming = (JObject)after.DeepClone();
        incoming["lastUpdated"] = Timestamp.AddMinutes(minutes);
        var config = new EntityConfig { MergeRules = [new MergeRule { DataSource = "*", SourceEntityType = "*", Context = "*", Rules = rules }] };
        var time = Substitute.For<ITimeService>();
        time.Now().Returns(Timestamp.AddDays(1));
        if (path == "first")
        {
            var response = await new ProcessNewEntityIntoExistingEntityRequestHandler(time).HandleAsync(new()
            {
                FromEntity = incoming, ToEntity = target, DataSource = "SRC", SourceEntityType = "Template",
                SourceEntityId = "source-1", EntityConfig = config
            }, CancellationToken.None);
            return (response.ResultingEntity, response.EntityUpdates);
        }

        JObject? changes = null;
        var stored = (JObject)target.DeepClone();
        var initial = new ChangeTrackingEntry
        {
            EntityId = "entity-1", EntityType = "Season", DataSource = DataSources.DataHub,
            EntryType = ChangeTrackingEntryTypes.Init, Timestamp = Timestamp.AddDays(-1), Data = (JObject)target.DeepClone()
        };
        var mediator = new TestMediator(request =>
        {
            switch (request)
            {
                case GetDataHubEntitiesByIdRequest:
                    return new GetDataHubEntitiesByIdResponse { Success = true, Results = [target] };
                case GetTrackingEntriesQuery:
                    return new PagedResults<ChangeTrackingEntry> { Results = [initial] };
                case CheckPreMergeRulesRequest:
                    return new CheckPreMergeRulesResponse { Pass = true };
                case MaterializeDataHubEntityRequest materialize:
                    return new MaterializeDataHubEntityResponse
                    {
                        ResultingEntity = ChangeTrackingHelper.ReassembleEntity(materialize.TrackingEntries).RemoveNullValues()
                    };
                case AddTrackedEntityChangeSetsRequest track:
                    changes = (JObject)track.Requests.Single().ChangeSet.DeepClone();
                    return new AddTrackedEntityChangeSetsResponse { Successes = [], Failures = [] };
                case UpsertDataHubEntitiesCommand upsert:
                    stored = (JObject)upsert.Entities.Single().DeepClone();
                    return new UpsertDataHubEntitiesResponse { Successes = upsert.Entities, Failures = [] };
                default:
                    throw new InvalidOperationException($"Unexpected request {request.GetType().Name}");
            }
        });
        var locks = Substitute.For<IProcessingLockService>();
        locks.WaitForLocksAsync(Arg.Any<List<string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>(), Arg.Any<TimeSpan?>())
            .Returns(Task.FromResult(new Response<List<ProcessingLock>>(true, [], null)));
        var references = new List<ResolvedEntityReference>
        {
            new()
            {
                SourceEntityReference = new ExternalEntityReference { DataSource = "SRC", SourceEntityType = "Template", EntityId = "source-1" },
                DataHubEntityReference = new EntityReference { EntityType = "Season", EntityId = "entity-1" }
            }
        };
        var mergeRequests = new List<MergeEntityRequest>
        {
            new() { DataSource = "SRC", SourceEntityType = "Template", SourceEntityId = "source-1", DataHubEntityType = "Season", Data = incoming }
        };
        List<MergeEntityResult> results;
        if (path == "tracked")
        {
            var delta = (JObject?)ChangeTrackingHelper.DiffIgnoringEquivalentValueTokens(before, after) ?? new JObject();
            var response = await new ProcessUpdatedEntitiesRequestHandler(Substitute.For<IIdService>(), mediator, locks, time).HandleAsync(new()
            {
                DataSource = "SRC", SourceEntityType = "Template", DataHubEntityType = "Season", EntityConfig = config,
                CorrelationId = "test", ResolvedReferencedEntities = references, MergeRequests = mergeRequests,
                SourceEntityChangesWithAlternateKeys = [],
                ConvertedSourceEntityChanges = [new ChangeTrackingEntry
                {
                    EntityId = "entity-1", EntityType = "Season", DataSource = DataSources.DataHub,
                    EntryType = ChangeTrackingEntryTypes.Update, Timestamp = Timestamp.AddMinutes(minutes), Data = delta
                }]
            }, CancellationToken.None);
            results = response.Results;
        }
        else
        {
            var response = await new ProcessUpdatedUntrackedEntitiesRequestHandler(mediator, locks, time).HandleAsync(new()
            {
                DataSource = "SRC", SourceEntityType = "Template", DataHubEntityType = "Season", EntityConfig = config,
                CorrelationId = "test", ResolvedReferencedEntities = references, MergeRequests = mergeRequests
            }, CancellationToken.None);
            results = response.Results;
        }
        results.Should().NotContain(r => r.MergeOutcome == MergeOutcomes.MergeFailed,
            "merging should succeed: {0}", string.Join("; ", results.Select(r => r.FailureReason)));
        return (stored, changes);
    }

    private sealed class TestMediator(Func<IRequest, object> respond) : IMediator
    {
        public Task<OneOf<TResponse, Exception>> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken)
            => Task.FromResult<OneOf<TResponse, Exception>>((TResponse)respond(request));
        public Task<OneOf<object, Exception>> SendAsync(IRequest request, CancellationToken cancellationToken)
            => Task.FromResult<OneOf<object, Exception>>(respond(request));
        public Task<(TResponse? Response, Exception? Exception)> TrySend<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken, Action<Exception>? exceptionHandler = null)
            => Task.FromResult(((TResponse?)respond(request), (Exception?)null));
        public Task<object> SendAndHandleExceptions<TRequest>(TRequest request, CancellationToken cancellationToken, Action<Exception>? exceptionHandler = null) where TRequest : IRequest
            => throw new NotSupportedException();
        public Task<TResponse> SendAndHandleExceptions<TResponse>(IRequest request, CancellationToken cancellationToken, Action<Exception>? exceptionHandler = null)
            => throw new NotSupportedException();
    }
}
