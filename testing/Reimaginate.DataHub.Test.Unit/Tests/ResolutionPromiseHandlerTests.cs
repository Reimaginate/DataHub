using FluentAssertions;
using Newtonsoft.Json.Linq;
using OneOf;
using Reimaginate.DataHub.DataAccess.Commands.DeleteCosmosDocuments;
using Reimaginate.DataHub.DataAccess.Commands.UpsertCosmosDocuments;
using Reimaginate.DataHub.DataAccess.Commands.UpsertDataHubEntities;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Models;
using Reimaginate.DataHub.DataAccess.Queries.FindMatchingEntities;
using Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocuments;
using Reimaginate.DataHub.Requests.External.Client.ResolveEntityReferences;
using Reimaginate.DataHub.Requests.External.CLI.DeleteResolutionPromises;
using Reimaginate.DataHub.Requests.External.CLI.GetResolutionPromises;
using Reimaginate.DataHub.Requests.External.CLI.ListResolutionPromises;
using Reimaginate.DataHub.Requests.External.CLI.PatchResolutionPromise;
using Reimaginate.DataHub.Requests.External.CLI.ResolveResolutionPromises;
using Reimaginate.DataHub.Requests.Internal.AddTrackedEntityChangeSets;
using Reimaginate.DataHub.Requests.Internal.CreateDeferredEntityResolutionPromises;
using Reimaginate.DataHub.Requests.Internal.FindEntitiesByAlternateKey;
using Reimaginate.DataHub.Requests.Internal.ProcessPatchEntities;
using Reimaginate.DataHub.Requests.Internal.ProcessPatchEntity;
using Reimaginate.DataHub.Requests.Internal.ResolveEntityReferenceResolutionPromises;
using Reimaginate.DataHub.Requests.Internal.ResolveExternalEntityReferences;
using Reimaginate.DataHub.Services.TimeService;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Exceptions;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Reimaginate.DataHub.SharedModels.Requests.CLI;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;
using Xunit;
using Reimaginate.DataHub.Test.Unit.Base;
using Reimaginate.Test.Framework;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class ResolutionPromiseHandlerTests : ScenarioUnitTestBase
{
    [Fact]
    public async Task CreateDeferredEntityResolutionPromises_should_create_promises_for_top_level_nested_and_array_references()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(CreateDeferredEntityResolutionPromises_should_create_promises_for_top_level_nested_and_array_references)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(CreateDeferredEntityResolutionPromises_should_create_promises_for_top_level_nested_and_array_references))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    UpsertCosmosDocumentsCommand<ResolutionPromise>? capturedCommand = null;
                    var mediator = new RecordingMediator(request =>
                    {
                        capturedCommand = request.Should().BeOfType<UpsertCosmosDocumentsCommand<ResolutionPromise>>().Subject;
                        return new UpsertCosmosDocumentsResponse<ResolutionPromise>
                        {
                            Successes = capturedCommand.Documents,
                            Failures = []
                        };
                    });
                    var handler = new CreateDeferredEntityResolutionPromisesRequestHandler(mediator);
                    var entity = Entity("owner-1", "OwnerType", new JObject
                    {
                        ["Parent"] = Reference("SRC1", "ParentType", "ParentDH", "parent-1"),
                        ["Nested"] = new JObject
                        {
                            ["Child"] = Reference("SRC1", "ChildType", "ChildDH", "child-1")
                        },
                        ["Children"] = new JArray
                        {
                            new JObject
                            {
                                ["Reference"] = Reference("SRC2", "ArrayType", "ArrayDH", "array-1")
                            }
                        }
                    });

                    var response = await handler.HandleAsync(new CreateDeferredEntityResolutionPromisesRequest
                    {
                        Entities = [entity]
                    }, CancellationToken.None);

                    response.ResultingPromises.Should().HaveCount(3);
                    capturedCommand.Should().NotBeNull();
                    capturedCommand!.Documents.Should().BeEquivalentTo(response.ResultingPromises);

                    response.ResultingPromises.Select(p => p.EntityReferencePath)
                        .Should()
                        .BeEquivalentTo("Parent", "Nested.Child", "Children[0].Reference");

                    var parentPromise = response.ResultingPromises.Single(p => p.EntityReferencePath == "Parent");
                    parentPromise.DataHubEntityType.Should().Be("OwnerType");
                    parentPromise.DataHubEntityId.Should().Be("owner-1");
                    parentPromise.ExternalEntityReference.DataSource.Should().Be("SRC1");
                    parentPromise.ExternalEntityReference.SourceEntityType.Should().Be("ParentType");
                    parentPromise.ExternalEntityReference.EntityType.Should().Be("ParentDH");
                    parentPromise.ExternalEntityReference.EntityId.Should().Be("parent-1");
                    parentPromise.id.Should().Be("ParentType:parent-1->Parent:OwnerType:owner-1");

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
    public async Task CreateDeferredEntityResolutionPromises_should_keep_deterministic_ids_across_repeated_calls()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(CreateDeferredEntityResolutionPromises_should_keep_deterministic_ids_across_repeated_calls)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(CreateDeferredEntityResolutionPromises_should_keep_deterministic_ids_across_repeated_calls))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var mediator = new RecordingMediator(request =>
                    {
                        var command = request.Should().BeOfType<UpsertCosmosDocumentsCommand<ResolutionPromise>>().Subject;
                        return new UpsertCosmosDocumentsResponse<ResolutionPromise> { Successes = command.Documents, Failures = [] };
                    });
                    var handler = new CreateDeferredEntityResolutionPromisesRequestHandler(mediator);
                    var request = new CreateDeferredEntityResolutionPromisesRequest
                    {
                        Entities = [Entity("owner-1", "OwnerType", new JObject { ["Parent"] = Reference("SRC1", "ParentType", "ParentDH", "parent-1") })]
                    };

                    var first = await handler.HandleAsync(request, CancellationToken.None);
                    var second = await handler.HandleAsync(request, CancellationToken.None);

                    second.ResultingPromises.Should().ContainSingle();
                    second.ResultingPromises[0].id.Should().Be(first.ResultingPromises[0].id);

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
    public async Task ResolveExternalEntityReferences_should_return_empty_response_when_entity_has_no_external_references()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(ResolveExternalEntityReferences_should_return_empty_response_when_entity_has_no_external_references)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(ResolveExternalEntityReferences_should_return_empty_response_when_entity_has_no_external_references))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var mediator = new RecordingMediator(_ => throw new InvalidOperationException("No mediator calls expected."));
                    var handler = new ResolveExternalEntityReferencesRequestHandler(mediator);

                    var response = await handler.HandleAsync(new ResolveExternalEntityReferencesRequest
                    {
                        DataHubEntitiesToResolve = [Entity("owner-1", "OwnerType")]
                    }, CancellationToken.None);

                    response.UpdatedDataHubEntities.Should().BeEmpty();
                    mediator.Requests.Should().BeEmpty();

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
    public async Task ResolveExternalEntityReferences_should_resolve_multiple_references_on_same_entity_and_track_changes()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(ResolveExternalEntityReferences_should_resolve_multiple_references_on_same_entity_and_track_changes)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(ResolveExternalEntityReferences_should_resolve_multiple_references_on_same_entity_and_track_changes))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    AddTrackedEntityChangeSetsRequest? capturedTracking = null;
                    UpsertDataHubEntitiesCommand? capturedUpsert = null;
                    var mediator = new RecordingMediator(request =>
                    {
                        switch (request)
                        {
                            case FindEntitiesByAlternateKeyRequest find:
                                return FoundEntity(find.Value, find.EntityType);
                            case AddTrackedEntityChangeSetsRequest addTracking:
                                capturedTracking = addTracking;
                                return new AddTrackedEntityChangeSetsResponse { Successes = [], Failures = [] };
                            case UpsertDataHubEntitiesCommand upsert:
                                capturedUpsert = upsert;
                                return new UpsertDataHubEntitiesResponse { Successes = upsert.Entities, Failures = [] };
                            default:
                                throw new InvalidOperationException(request.GetType().FullName);
                        }
                    });
                    var handler = new ResolveExternalEntityReferencesRequestHandler(mediator);
                    var entity = Entity("owner-1", "OwnerType", new JObject
                    {
                        ["Primary"] = Reference("SRC1", "TypeA", "ParentDH", "parent-1", DateTimeOffset.Parse("2024-01-01T00:00:00Z")),
                        ["Children"] = new JArray
                        {
                            new JObject
                            {
                                ["Reference"] = Reference("SRC1", "TypeB", "ChildDH", "child-1", DateTimeOffset.Parse("2024-01-02T00:00:00Z"))
                            }
                        }
                    });

                    var response = await handler.HandleAsync(new ResolveExternalEntityReferencesRequest
                    {
                        DataHubEntitiesToResolve = [entity]
                    }, CancellationToken.None);

                    var updated = response.UpdatedDataHubEntities.Should().ContainSingle().Subject;
                    AssertResolvedReference(updated, "Primary", "ParentDH", "dh-parent-1");
                    AssertResolvedReference(updated, "Children[0].Reference", "ChildDH", "dh-child-1");

                    capturedUpsert.Should().NotBeNull();
                    capturedUpsert!.Entities.Should().ContainSingle();
                    capturedTracking.Should().NotBeNull();
                    capturedTracking!.Requests.Should().HaveCount(2);
                    capturedTracking.Requests.Select(r => r.DataSource).Should().OnlyContain(dataSource => dataSource == DataSources.DataHub);
                    capturedTracking.Requests.Select(r => r.TimeStamp)
                        .Should()
                        .BeEquivalentTo(new[]
                        {
                            DateTimeOffset.Parse("2024-01-01T00:00:00Z"),
                            DateTimeOffset.Parse("2024-01-02T00:00:00Z")
                        });

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
    public async Task ResolveExternalEntityReferences_should_skip_tracking_when_do_not_track_is_true()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(ResolveExternalEntityReferences_should_skip_tracking_when_do_not_track_is_true)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(ResolveExternalEntityReferences_should_skip_tracking_when_do_not_track_is_true))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var mediator = new RecordingMediator(request =>
                    {
                        return request switch
                        {
                            FindEntitiesByAlternateKeyRequest find => FoundEntity(find.Value, find.EntityType),
                            UpsertDataHubEntitiesCommand upsert => new UpsertDataHubEntitiesResponse { Successes = upsert.Entities, Failures = [] },
                            AddTrackedEntityChangeSetsRequest => throw new InvalidOperationException("Tracking should be skipped."),
                            _ => throw new InvalidOperationException(request.GetType().FullName)
                        };
                    });
                    var handler = new ResolveExternalEntityReferencesRequestHandler(mediator);

                    var response = await handler.HandleAsync(new ResolveExternalEntityReferencesRequest
                    {
                        DataHubEntitiesToResolve = [Entity("owner-1", "OwnerType", new JObject { ["Parent"] = Reference("SRC1", "TypeA", "ParentDH", "parent-1") })],
                        DoNotTrack = true
                    }, CancellationToken.None);

                    response.UpdatedDataHubEntities.Should().ContainSingle();
                    mediator.Requests.Should().NotContain(request => request is AddTrackedEntityChangeSetsRequest);

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
    public async Task ResolveExternalEntityReferences_should_leave_unmatched_reference_unchanged()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(ResolveExternalEntityReferences_should_leave_unmatched_reference_unchanged)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(ResolveExternalEntityReferences_should_leave_unmatched_reference_unchanged))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var mediator = new RecordingMediator(request =>
                    {
                        return request switch
                        {
                            FindEntitiesByAlternateKeyRequest => new JArray(),
                            UpsertDataHubEntitiesCommand => throw new InvalidOperationException("Unmatched references should not be upserted."),
                            AddTrackedEntityChangeSetsRequest => throw new InvalidOperationException("Unmatched references should not be tracked."),
                            _ => throw new InvalidOperationException(request.GetType().FullName)
                        };
                    });
                    var handler = new ResolveExternalEntityReferencesRequestHandler(mediator);

                    var response = await handler.HandleAsync(new ResolveExternalEntityReferencesRequest
                    {
                        DataHubEntitiesToResolve = [Entity("owner-1", "OwnerType", new JObject { ["Parent"] = Reference("SRC1", "TypeA", "ParentDH", "missing") })]
                    }, CancellationToken.None);

                    response.UpdatedDataHubEntities.Should().BeEmpty();

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
    public async Task ResolveExternalEntityReferences_should_fail_when_alternate_key_matches_multiple_entities()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(ResolveExternalEntityReferences_should_fail_when_alternate_key_matches_multiple_entities)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(ResolveExternalEntityReferences_should_fail_when_alternate_key_matches_multiple_entities))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var mediator = new RecordingMediator(request =>
                    {
                        return request switch
                        {
                            FindEntitiesByAlternateKeyRequest find => new JArray(FoundEntity(find.Value, find.EntityType)[0], FoundEntity(find.Value, find.EntityType)[0]),
                            _ => throw new InvalidOperationException(request.GetType().FullName)
                        };
                    });
                    var handler = new ResolveExternalEntityReferencesRequestHandler(mediator);

                    var act = () => handler.HandleAsync(new ResolveExternalEntityReferencesRequest
                    {
                        DataHubEntitiesToResolve = [Entity("owner-1", "OwnerType", new JObject { ["Parent"] = Reference("SRC1", "TypeA", "ParentDH", "parent-1") })]
                    }, CancellationToken.None);

                    await act.Should().ThrowAsync<InvalidOperationException>()
                        .WithMessage("Multiple entities found with matching alternate keys");

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
    public async Task ResolveEntityReferences_client_handler_should_batch_projected_lookups_and_preserve_duplicate_results()
    {
        var uniqueReference = new ExternalEntityReference
        {
            DataSource = "SRC1",
            SourceEntityType = "TypeA",
            EntityType = "TargetType",
            EntityId = "source-unique"
        };
        var duplicateReference = new ExternalEntityReference
        {
            DataSource = "SRC1",
            SourceEntityType = "TypeA",
            EntityType = "TargetType",
            EntityId = "source-duplicate"
        };
        var mediator = new RecordingMediator(request => request switch
        {
            FindMatchingEntitiesQuery => new JArray(
                FoundEntity("source-unique", "TargetType")[0],
                FoundEntity("source-duplicate", "TargetType")[0],
                FoundEntity("source-duplicate", "TargetType", "dh-source-duplicate-2")[0]),
            _ => throw new InvalidOperationException($"Unexpected mediator request {request.GetType().Name}")
        });

        var response = await new ResolveEntityReferencesRequestHandler(mediator).HandleAsync(new ResolveEntityReferencesRequest
        {
            EntityReferences = [uniqueReference, duplicateReference]
        }, CancellationToken.None);

        response.Success.Should().BeTrue();
        response.Results.Should().HaveCount(2);
        response.ResolutionFailures.Should().ContainSingle();
        response.Results.Single(result => result.SourceEntityReference == uniqueReference)
            .DataHubEntityReference.EntityId.Should().Be("dh-source-unique");
        response.Results.Single(result => result.SourceEntityReference == duplicateReference)
            .DataHubEntityReference.Should().BeOfType<ExternalEntityReference>()
            .Which._tag.Should().BeNull();

        var lookupQuery = mediator.Requests.OfType<FindMatchingEntitiesQuery>().Should().ContainSingle().Subject;
        lookupQuery.SelectClause.Should().Be("x.id,x.entityType,x.alternateKeys");
        lookupQuery.Parameters
            .Where(parameter => parameter.Name.StartsWith("entityId", StringComparison.Ordinal))
            .Select(parameter => parameter.Value?.ToString())
            .Should()
            .BeEquivalentTo("source-unique", "source-duplicate");
    }

    [Fact]
    public async Task ResolveEntityReferences_client_handler_should_limit_projected_lookup_batches_to_500_values()
    {
        var references = Enumerable.Range(1, 501)
            .Select(index => new ExternalEntityReference
            {
                DataSource = "SRC1",
                SourceEntityType = "TypeA",
                EntityType = "TargetType",
                EntityId = $"source-{index}"
            })
            .ToList();
        var mediator = new RecordingMediator(request => request switch
        {
            FindMatchingEntitiesQuery find => new JArray(find.Parameters
                .Where(parameter => parameter.Name.StartsWith("entityId", StringComparison.Ordinal))
                .Select(parameter => FoundEntity(parameter.Value!.ToString()!, find.EntityType)[0])),
            _ => throw new InvalidOperationException($"Unexpected mediator request {request.GetType().Name}")
        });

        var response = await new ResolveEntityReferencesRequestHandler(mediator).HandleAsync(new ResolveEntityReferencesRequest
        {
            EntityReferences = references
        }, CancellationToken.None);

        response.Success.Should().BeTrue();
        response.Results.Should().HaveCount(501);
        mediator.Requests.OfType<FindMatchingEntitiesQuery>()
            .Select(query => query.Parameters.Count(parameter => parameter.Name.StartsWith("entityId", StringComparison.Ordinal)))
            .Should().Equal(500, 1);
        mediator.Requests.OfType<FindMatchingEntitiesQuery>()
            .Should().OnlyContain(query => query.SelectClause == "x.id,x.entityType,x.alternateKeys");
    }

    [Fact]
    public async Task ResolveEntityReferenceResolutionPromises_should_resolve_across_pages_delete_resolved_subset_and_track_with_reference_timestamp()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(ResolveEntityReferenceResolutionPromises_should_resolve_across_pages_delete_resolved_subset_and_track_with_reference_timestamp)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(ResolveEntityReferenceResolutionPromises_should_resolve_across_pages_delete_resolved_subset_and_track_with_reference_timestamp))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    AddTrackedEntityChangeSetsRequest? capturedTracking = null;
                    UpsertDataHubEntitiesCommand? capturedUpsert = null;
                    DeleteCosmosDocumentsCommand<ResolutionPromise>? capturedDelete = null;
                    var page1 = new List<ResolutionPromise>
                    {
                        Promise("promise-1", "OwnerType", "owner-1", "Parent", "SRC1", "TypeA", "ParentDH", "parent-1", DateTimeOffset.Parse("2024-02-01T00:00:00Z"))
                    };
                    var page2 = new List<ResolutionPromise>
                    {
                        Promise("promise-2", "OwnerType", "owner-2", "Parent", "SRC1", "TypeA", "ParentDH", "unresolved")
                    };
                    var owners = new List<JObject>
                    {
                        Entity("owner-1", "OwnerType", new JObject { ["Parent"] = Reference("SRC1", "TypeA", "ParentDH", "parent-1") }),
                        Entity("owner-2", "OwnerType", new JObject { ["Parent"] = Reference("SRC1", "TypeA", "ParentDH", "unresolved") })
                    };
                    var mediator = new RecordingMediator(request =>
                    {
                        switch (request)
                        {
                            case GetCosmosDocumentsQuery<ResolutionPromise> query:
                                return string.IsNullOrEmpty(query.ContinuationToken)
                                    ? new PagedResults<ResolutionPromise> { Results = page1, MoreResultsAvailable = true, ContinuationToken = "page-2" }
                                    : new PagedResults<ResolutionPromise> { Results = page2, MoreResultsAvailable = false };
                            case GetDataHubEntitiesByIdRequest:
                                return new GetDataHubEntitiesByIdResponse { Results = owners };
                            case AddTrackedEntityChangeSetsRequest addTracking:
                                capturedTracking = addTracking;
                                return new AddTrackedEntityChangeSetsResponse { Successes = [], Failures = [] };
                            case UpsertDataHubEntitiesCommand upsert:
                                capturedUpsert = upsert;
                                return new UpsertDataHubEntitiesResponse { Successes = upsert.Entities, Failures = [] };
                            case DeleteCosmosDocumentsCommand<ResolutionPromise> delete:
                                capturedDelete = delete;
                                return new DeleteCosmosDocumentsResponse<ResolutionPromise> { Successes = delete.Documents, Failures = [] };
                            default:
                                throw new InvalidOperationException(request.GetType().FullName);
                        }
                    });
                    var handler = new ResolveEntityReferenceResolutionPromisesRequestHandler(mediator, new FixedTimeService(DateTimeOffset.Parse("2024-12-01T00:00:00Z")));

                    var response = await handler.HandleAsync(new ResolveEntityReferenceResolutionPromisesRequest
                    {
                        SourceSystemEntityIds = ["parent-1", "unresolved"],
                        ResolvedReferencedEntities =
                        [
                            Resolved("SRC1", "TypeA", "parent-1", "ParentDH", "dh-parent-1")
                        ]
                    }, CancellationToken.None);

                    response.UpdatedDataHubEntities.Should().ContainSingle(entity => entity.Value<string>(nameof(DataHubEntity.id)) == "owner-1");
                    AssertResolvedReference(response.UpdatedDataHubEntities.Single(), "Parent", "ParentDH", "dh-parent-1");

                    capturedTracking.Should().NotBeNull();
                    capturedTracking!.Requests.Should().ContainSingle().Which.TimeStamp.Should().Be(DateTimeOffset.Parse("2024-02-01T00:00:00Z"));
                    capturedUpsert.Should().NotBeNull();
                    capturedUpsert!.Entities.Should().ContainSingle(entity => entity.Value<string>(nameof(DataHubEntity.id)) == "owner-1");
                    capturedDelete.Should().NotBeNull();
                    capturedDelete!.Documents.Should().ContainSingle(p => p.id == "promise-1");

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
    public async Task ResolveEntityReferenceResolutionPromises_should_skip_tracking_when_do_not_track_is_true()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(ResolveEntityReferenceResolutionPromises_should_skip_tracking_when_do_not_track_is_true)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(ResolveEntityReferenceResolutionPromises_should_skip_tracking_when_do_not_track_is_true))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var mediator = new RecordingMediator(request =>
                    {
                        return request switch
                        {
                            GetCosmosDocumentsQuery<ResolutionPromise> => new PagedResults<ResolutionPromise>
                            {
                                Results = [Promise("promise-1", "OwnerType", "owner-1", "Parent", "SRC1", "TypeA", "ParentDH", "parent-1")]
                            },
                            GetDataHubEntitiesByIdRequest => new GetDataHubEntitiesByIdResponse
                            {
                                Results = [Entity("owner-1", "OwnerType", new JObject { ["Parent"] = Reference("SRC1", "TypeA", "ParentDH", "parent-1") })]
                            },
                            UpsertDataHubEntitiesCommand upsert => new UpsertDataHubEntitiesResponse { Successes = upsert.Entities, Failures = [] },
                            DeleteCosmosDocumentsCommand<ResolutionPromise> delete => new DeleteCosmosDocumentsResponse<ResolutionPromise> { Successes = delete.Documents, Failures = [] },
                            AddTrackedEntityChangeSetsRequest => throw new InvalidOperationException("Tracking should be skipped."),
                            _ => throw new InvalidOperationException(request.GetType().FullName)
                        };
                    });
                    var handler = new ResolveEntityReferenceResolutionPromisesRequestHandler(mediator, new FixedTimeService(DateTimeOffset.Parse("2024-12-01T00:00:00Z")));

                    var response = await handler.HandleAsync(new ResolveEntityReferenceResolutionPromisesRequest
                    {
                        SourceSystemEntityIds = ["parent-1"],
                        ResolvedReferencedEntities = [Resolved("SRC1", "TypeA", "parent-1", "ParentDH", "dh-parent-1")],
                        DoNotTrack = true
                    }, CancellationToken.None);

                    response.UpdatedDataHubEntities.Should().ContainSingle();
                    mediator.Requests.Should().NotContain(request => request is AddTrackedEntityChangeSetsRequest);

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
    public async Task ResolveEntityReferenceResolutionPromises_should_resolve_more_than_one_page_of_promises()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(ResolveEntityReferenceResolutionPromises_should_resolve_more_than_one_page_of_promises)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(ResolveEntityReferenceResolutionPromises_should_resolve_more_than_one_page_of_promises))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    const int firstPageCount = 1000;
                    const int secondPageCount = 1;
                    var allPromises = Enumerable.Range(0, firstPageCount + secondPageCount)
                        .Select(index => Promise($"promise-{index}", "OwnerType", $"owner-{index}", "Parent", "SRC1", "TypeA", "ParentDH", $"parent-{index}"))
                        .ToList();
                    var owners = allPromises
                        .Select(p => Entity(p.DataHubEntityId, p.DataHubEntityType, new JObject
                        {
                            [p.EntityReferencePath] = Reference(p.ExternalEntityReference.DataSource, p.ExternalEntityReference.SourceEntityType, p.ExternalEntityReference.EntityType, p.ExternalEntityReference.EntityId)
                        }))
                        .ToList();
                    var capturedDeletes = new List<DeleteCosmosDocumentsCommand<ResolutionPromise>>();
                    var mediator = new RecordingMediator(request =>
                    {
                        switch (request)
                        {
                            case GetCosmosDocumentsQuery<ResolutionPromise> query:
                                return string.IsNullOrEmpty(query.ContinuationToken)
                                    ? new PagedResults<ResolutionPromise> { Results = allPromises.Take(firstPageCount).ToList(), MoreResultsAvailable = true, ContinuationToken = "page-2" }
                                    : new PagedResults<ResolutionPromise> { Results = allPromises.Skip(firstPageCount).ToList(), MoreResultsAvailable = false };
                            case GetDataHubEntitiesByIdRequest:
                                return new GetDataHubEntitiesByIdResponse { Results = owners };
                            case AddTrackedEntityChangeSetsRequest addTracking:
                                return new AddTrackedEntityChangeSetsResponse { Successes = addTracking.Requests.Select(r => new ChangeTrackingEntry()).ToList(), Failures = [] };
                            case UpsertDataHubEntitiesCommand upsert:
                                return new UpsertDataHubEntitiesResponse { Successes = upsert.Entities, Failures = [] };
                            case DeleteCosmosDocumentsCommand<ResolutionPromise> delete:
                                capturedDeletes.Add(delete);
                                return new DeleteCosmosDocumentsResponse<ResolutionPromise> { Successes = delete.Documents, Failures = [] };
                            default:
                                throw new InvalidOperationException(request.GetType().FullName);
                        }
                    });
                    var handler = new ResolveEntityReferenceResolutionPromisesRequestHandler(mediator, new FixedTimeService(DateTimeOffset.Parse("2024-12-01T00:00:00Z")));

                    var response = await handler.HandleAsync(new ResolveEntityReferenceResolutionPromisesRequest
                    {
                        SourceSystemEntityIds = allPromises.Select(p => p.ExternalEntityReference.EntityId).ToList(),
                        ResolvedReferencedEntities = allPromises
                            .Select(p => Resolved(p.ExternalEntityReference.DataSource, p.ExternalEntityReference.SourceEntityType, p.ExternalEntityReference.EntityId, p.ExternalEntityReference.EntityType, $"dh-{p.ExternalEntityReference.EntityId}"))
                            .ToList()
                    }, CancellationToken.None);

                    response.UpdatedDataHubEntities.Should().HaveCount(firstPageCount + secondPageCount);
                    capturedDeletes.SelectMany(delete => delete.Documents).Should().HaveCount(firstPageCount + secondPageCount);
                    capturedDeletes.Should().OnlyContain(delete => delete.Documents.Count <= 500);
                    mediator.Requests.OfType<AddTrackedEntityChangeSetsRequest>().Should().OnlyContain(addTracking => addTracking.Requests.Count <= 100);
                    mediator.Requests.OfType<UpsertDataHubEntitiesCommand>().Should().OnlyContain(upsert => upsert.Entities.Count <= 100);
                    mediator.Requests.OfType<GetCosmosDocumentsQuery<ResolutionPromise>>().Should().HaveCount(2);

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
    public async Task ResolveResolutionPromises_cli_handler_should_resolve_by_id_update_owner_track_and_delete_promise()
    {
        var promise = Promise("promise-1", "OwnerType", "owner-1", "Parent", "SRC1", "TypeA", "TargetType", "source-1", DateTimeOffset.Parse("2024-06-01T00:00:00Z"));
        var owner = Entity("owner-1", "OwnerType", new JObject
        {
            ["Parent"] = Reference("SRC1", "TypeA", "TargetType", "source-1")
        });
        AddTrackedEntityChangeSetsRequest? capturedTracking = null;
        UpsertDataHubEntitiesCommand? capturedUpsert = null;
        DeleteCosmosDocumentsCommand<ResolutionPromise>? capturedDelete = null;
        var mediator = new RecordingMediator(request => request switch
        {
            GetCosmosDocumentsQuery<ResolutionPromise> => new PagedResults<ResolutionPromise> { Results = [promise] },
            GetDataHubEntitiesByIdRequest => new GetDataHubEntitiesByIdResponse { Results = [owner] },
            FindMatchingEntitiesQuery find => FoundEntity(LookupValue(find), find.EntityType),
            AddTrackedEntityChangeSetsRequest tracking => Capture(tracking, ref capturedTracking, TrackingSuccess()),
            UpsertDataHubEntitiesCommand upsert => Capture(upsert, ref capturedUpsert, UpsertSuccess(upsert)),
            DeleteCosmosDocumentsCommand<ResolutionPromise> delete => CaptureDelete(delete, ref capturedDelete),
            _ => throw new InvalidOperationException($"Unexpected mediator request {request.GetType().Name}")
        });

        var response = await new ResolveResolutionPromisesRequestHandler(mediator).HandleAsync(new ResolveResolutionPromisesRequest
        {
            PromiseIds = ["promise-1"]
        }, CancellationToken.None);

        response.MatchedCount.Should().Be(1);
        response.ResolvedCount.Should().Be(1);
        response.Results.Single().Status.Should().Be("Resolved");
        response.Results.Single().ResolvedEntityId.Should().Be("dh-source-1");
        capturedTracking.Should().NotBeNull();
        capturedTracking!.Requests.Should().ContainSingle().Which.TimeStamp.Should().Be(DateTimeOffset.Parse("2024-06-01T00:00:00Z"));
        capturedUpsert.Should().NotBeNull();
        AssertResolvedReference(capturedUpsert!.Entities.Single(), "Parent", "TargetType", "dh-source-1");
        capturedDelete!.Documents.Should().ContainSingle().Which.id.Should().Be("promise-1");
        mediator.Requests.Should().NotContain(request => request is ProcessPatchEntitiesRequest);
    }

    [Fact]
    public async Task ResolveResolutionPromises_cli_handler_should_tolerate_an_already_deleted_promise()
    {
        var promise = Promise("promise-1", "OwnerType", "owner-1", "Parent", "SRC1", "TypeA", "TargetType", "source-1");
        var owner = Entity("owner-1", "OwnerType", new JObject
        {
            ["Parent"] = Reference("SRC1", "TypeA", "TargetType", "source-1")
        });
        var mediator = new RecordingMediator(request => request switch
        {
            GetCosmosDocumentsQuery<ResolutionPromise> => new PagedResults<ResolutionPromise> { Results = [promise] },
            GetDataHubEntitiesByIdRequest => new GetDataHubEntitiesByIdResponse { Results = [owner] },
            FindMatchingEntitiesQuery find => FoundEntity(LookupValue(find), find.EntityType),
            AddTrackedEntityChangeSetsRequest => TrackingSuccess(),
            UpsertDataHubEntitiesCommand upsert => UpsertSuccess(upsert),
            DeleteCosmosDocumentsCommand<ResolutionPromise> => new DeleteCosmosDocumentsResponse<ResolutionPromise>
            {
                Successes = [],
                Failures =
                [
                    new DataAccessFailure<ResolutionPromise>(
                        promise,
                        new InvalidOperationException("Response status code does not indicate success: NotFound (404)."))
                ]
            },
            _ => throw new InvalidOperationException($"Unexpected mediator request {request.GetType().Name}")
        });

        var response = await new ResolveResolutionPromisesRequestHandler(mediator).HandleAsync(new ResolveResolutionPromisesRequest
        {
            PromiseIds = ["promise-1"]
        }, CancellationToken.None);

        response.ResolvedCount.Should().Be(1);
        response.Results.Should().ContainSingle().Which.Status.Should().Be("Resolved");
    }

    [Fact]
    public async Task ResolveResolutionPromises_cli_handler_should_leave_unresolved_promise_when_target_is_missing()
    {
        var promise = Promise("promise-1", "OwnerType", "owner-1", "Parent", "SRC1", "TypeA", "TargetType", "source-1");
        var owner = Entity("owner-1", "OwnerType", new JObject
        {
            ["Parent"] = Reference("SRC1", "TypeA", "TargetType", "source-1")
        });
        var mediator = new RecordingMediator(request => request switch
        {
            GetCosmosDocumentsQuery<ResolutionPromise> => new PagedResults<ResolutionPromise> { Results = [promise] },
            GetDataHubEntitiesByIdRequest => new GetDataHubEntitiesByIdResponse { Results = [owner] },
            FindMatchingEntitiesQuery => new JArray(),
            _ => throw new InvalidOperationException($"Unexpected mediator request {request.GetType().Name}")
        });

        var response = await new ResolveResolutionPromisesRequestHandler(mediator).HandleAsync(new ResolveResolutionPromisesRequest
        {
            PromiseIds = ["promise-1"]
        }, CancellationToken.None);

        response.MatchedCount.Should().Be(1);
        response.UnresolvedCount.Should().Be(1);
        response.Results.Single().Status.Should().Be("Unresolved");
        mediator.Requests.Should().NotContain(request => request is ProcessPatchEntitiesRequest);
        mediator.Requests.Should().NotContain(request => request is UpsertDataHubEntitiesCommand);
        mediator.Requests.Should().NotContain(request => request is DeleteCosmosDocumentsCommand<ResolutionPromise>);
        mediator.Requests.Should().NotContain(request => request is AddTrackedEntityChangeSetsRequest);
    }

    [Fact]
    public async Task ResolveResolutionPromises_cli_handler_should_skip_duplicate_target_failures_by_default_and_continue()
    {
        var failedPromise = Promise("promise-duplicate", "OwnerType", "owner-duplicate", "Parent", "SRC1", "TypeA", "TargetType", "source-duplicate");
        var resolvedPromise = Promise("promise-resolved", "OwnerType", "owner-resolved", "Parent", "SRC1", "TypeA", "TargetType", "source-resolved");
        var unresolvedPromise = Promise("promise-unresolved", "OwnerType", "owner-unresolved", "Parent", "SRC1", "TypeA", "TargetType", "source-unresolved");
        var stalePromise = Promise("promise-stale", "OwnerType", "owner-stale", "Parent", "SRC1", "TypeA", "TargetType", "source-stale");
        var owners = new Dictionary<string, JObject>
        {
            ["owner-duplicate"] = Entity("owner-duplicate", "OwnerType", new JObject { ["Parent"] = Reference("SRC1", "TypeA", "TargetType", "source-duplicate") }),
            ["owner-resolved"] = Entity("owner-resolved", "OwnerType", new JObject { ["Parent"] = Reference("SRC1", "TypeA", "TargetType", "source-resolved") }),
            ["owner-unresolved"] = Entity("owner-unresolved", "OwnerType", new JObject { ["Parent"] = Reference("SRC1", "TypeA", "TargetType", "source-unresolved") })
        };
        UpsertDataHubEntitiesCommand? capturedUpsert = null;
        DeleteCosmosDocumentsCommand<ResolutionPromise>? capturedDelete = null;
        var mediator = new RecordingMediator(request => request switch
        {
            GetCosmosDocumentsQuery<ResolutionPromise> => new PagedResults<ResolutionPromise> { Results = [failedPromise, resolvedPromise, unresolvedPromise, stalePromise] },
            GetDataHubEntitiesByIdRequest getOwners => new GetDataHubEntitiesByIdResponse
            {
                Results = getOwners.EntityIds.Where(owners.ContainsKey).Select(entityId => owners[entityId]).ToList()
            },
            FindMatchingEntitiesQuery => new JArray(
                FoundEntity("source-duplicate", "TargetType")[0],
                FoundEntity("source-duplicate", "TargetType", "dh-source-duplicate-2")[0],
                FoundEntity("source-resolved", "TargetType")[0]),
            AddTrackedEntityChangeSetsRequest => TrackingSuccess(),
            UpsertDataHubEntitiesCommand upsert => Capture(upsert, ref capturedUpsert, UpsertSuccess(upsert)),
            DeleteCosmosDocumentsCommand<ResolutionPromise> delete => CaptureDelete(delete, ref capturedDelete),
            _ => throw new InvalidOperationException($"Unexpected mediator request {request.GetType().Name}")
        });

        var response = await new ResolveResolutionPromisesRequestHandler(mediator).HandleAsync(new ResolveResolutionPromisesRequest
        {
            PromiseIds = ["promise-duplicate", "promise-resolved", "promise-unresolved", "promise-stale"]
        }, CancellationToken.None);

        response.MatchedCount.Should().Be(4);
        response.ResolvedCount.Should().Be(1);
        response.UnresolvedCount.Should().Be(1);
        response.DeletedStaleCount.Should().Be(1);
        response.FailedCount.Should().Be(1);
        response.Results.Single(result => result.PromiseId == "promise-duplicate").Status.Should().Be("Failed");
        response.Results.Single(result => result.PromiseId == "promise-duplicate").Reason.Should().Be("Multiple entities found with matching alternate keys");
        response.Results.Single(result => result.PromiseId == "promise-resolved").Status.Should().Be("Resolved");
        response.Results.Single(result => result.PromiseId == "promise-unresolved").Status.Should().Be("Unresolved");
        response.Results.Single(result => result.PromiseId == "promise-stale").Status.Should().Be("Stale");
        capturedUpsert.Should().NotBeNull();
        capturedUpsert!.Entities.Should().ContainSingle().Which.DataHubEntityId().Should().Be("owner-resolved");
        capturedDelete.Should().NotBeNull();
        capturedDelete!.Documents.Select(promise => promise.id).Should().BeEquivalentTo(["promise-resolved", "promise-stale"]);
        var lookupQuery = mediator.Requests.OfType<FindMatchingEntitiesQuery>().Should().ContainSingle().Subject;
        lookupQuery.Parameters
            .Where(parameter => parameter.Name.StartsWith("entityId", StringComparison.Ordinal))
            .Select(parameter => parameter.Value?.ToString())
            .Should()
            .BeEquivalentTo("source-duplicate", "source-resolved", "source-unresolved");
        mediator.Requests.Should().NotContain(request => request is ProcessPatchEntitiesRequest);
    }

    [Fact]
    public async Task ResolveResolutionPromises_cli_handler_should_stop_on_duplicate_target_failure_when_requested()
    {
        var promise = Promise("promise-duplicate", "OwnerType", "owner-duplicate", "Parent", "SRC1", "TypeA", "TargetType", "source-duplicate");
        var owner = Entity("owner-duplicate", "OwnerType", new JObject
        {
            ["Parent"] = Reference("SRC1", "TypeA", "TargetType", "source-duplicate")
        });
        var mediator = new RecordingMediator(request => request switch
        {
            GetCosmosDocumentsQuery<ResolutionPromise> => new PagedResults<ResolutionPromise> { Results = [promise] },
            GetDataHubEntitiesByIdRequest => new GetDataHubEntitiesByIdResponse { Results = [owner] },
            FindMatchingEntitiesQuery => new JArray(
                FoundEntity("source-duplicate", "TargetType")[0],
                FoundEntity("source-duplicate", "TargetType", "dh-source-duplicate-2")[0]),
            _ => throw new InvalidOperationException($"Unexpected mediator request {request.GetType().Name}")
        });

        var act = () => new ResolveResolutionPromisesRequestHandler(mediator).HandleAsync(new ResolveResolutionPromisesRequest
        {
            PromiseIds = ["promise-duplicate"],
            StopOnFailure = true
        }, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<DataHubInvalidRequestException>();
        exception.Which.Message.Should().Be("Multiple entities found with matching alternate keys");
        exception.Which.RequestType.Should().Be(nameof(ResolveResolutionPromisesRequest));
        exception.Which.Details.Should().Contain(detail =>
            detail.Field == "AlternateKey" &&
            detail.Code == "DuplicateMatch" &&
            detail.Message == "TargetType:src1.typea=source-duplicate matched 2 entities.");
        mediator.Requests.Should().NotContain(request => request is AddTrackedEntityChangeSetsRequest);
        mediator.Requests.Should().NotContain(request => request is UpsertDataHubEntitiesCommand);
        mediator.Requests.Should().NotContain(request => request is DeleteCosmosDocumentsCommand<ResolutionPromise>);
        mediator.Requests.Should().NotContain(request => request is ProcessPatchEntitiesRequest);
    }

    [Fact]
    public async Task ResolveResolutionPromises_cli_handler_should_report_duplicate_target_failure_in_dry_run_without_writes()
    {
        var promise = Promise("promise-duplicate", "OwnerType", "owner-duplicate", "Parent", "SRC1", "TypeA", "TargetType", "source-duplicate");
        var owner = Entity("owner-duplicate", "OwnerType", new JObject
        {
            ["Parent"] = Reference("SRC1", "TypeA", "TargetType", "source-duplicate")
        });
        var mediator = new RecordingMediator(request => request switch
        {
            GetCosmosDocumentsQuery<ResolutionPromise> => new PagedResults<ResolutionPromise> { Results = [promise] },
            GetDataHubEntitiesByIdRequest => new GetDataHubEntitiesByIdResponse { Results = [owner] },
            FindMatchingEntitiesQuery => new JArray(
                FoundEntity("source-duplicate", "TargetType")[0],
                FoundEntity("source-duplicate", "TargetType", "dh-source-duplicate-2")[0]),
            _ => throw new InvalidOperationException($"Unexpected mediator request {request.GetType().Name}")
        });

        var response = await new ResolveResolutionPromisesRequestHandler(mediator).HandleAsync(new ResolveResolutionPromisesRequest
        {
            PromiseIds = ["promise-duplicate"],
            DryRun = true
        }, CancellationToken.None);

        response.MatchedCount.Should().Be(1);
        response.FailedCount.Should().Be(1);
        response.Results.Should().ContainSingle().Which.Status.Should().Be("Failed");
        response.Results.Single().Reason.Should().Be("Multiple entities found with matching alternate keys");
        mediator.Requests.Should().NotContain(request => request is AddTrackedEntityChangeSetsRequest);
        mediator.Requests.Should().NotContain(request => request is UpsertDataHubEntitiesCommand);
        mediator.Requests.Should().NotContain(request => request is DeleteCosmosDocumentsCommand<ResolutionPromise>);
        mediator.Requests.Should().NotContain(request => request is ProcessPatchEntitiesRequest);
    }

    [Fact]
    public async Task ResolveResolutionPromises_cli_handler_should_report_dry_run_without_writes()
    {
        var promise = Promise("promise-1", "OwnerType", "owner-1", "Parent", "SRC1", "TypeA", "TargetType", "source-1");
        var owner = Entity("owner-1", "OwnerType", new JObject
        {
            ["Parent"] = Reference("SRC1", "TypeA", "TargetType", "source-1")
        });
        var mediator = new RecordingMediator(request => request switch
        {
            GetCosmosDocumentsQuery<ResolutionPromise> => new PagedResults<ResolutionPromise> { Results = [promise] },
            GetDataHubEntitiesByIdRequest => new GetDataHubEntitiesByIdResponse { Results = [owner] },
            FindMatchingEntitiesQuery find => FoundEntity(LookupValue(find), find.EntityType),
            _ => throw new InvalidOperationException($"Unexpected mediator request {request.GetType().Name}")
        });

        var response = await new ResolveResolutionPromisesRequestHandler(mediator).HandleAsync(new ResolveResolutionPromisesRequest
        {
            PromiseIds = ["promise-1"],
            DryRun = true
        }, CancellationToken.None);

        response.ResolvedCount.Should().Be(1);
        response.Results.Single().Status.Should().Be("Resolved");
        mediator.Requests.Should().NotContain(request => request is AddTrackedEntityChangeSetsRequest);
        mediator.Requests.Should().NotContain(request => request is UpsertDataHubEntitiesCommand);
        mediator.Requests.Should().NotContain(request => request is DeleteCosmosDocumentsCommand<ResolutionPromise>);
        mediator.Requests.Should().NotContain(request => request is ProcessPatchEntitiesRequest);
    }

    [Fact]
    public async Task ResolveResolutionPromises_cli_handler_should_skip_tracking_when_do_not_track_is_true()
    {
        var promise = Promise("promise-1", "OwnerType", "owner-1", "Parent", "SRC1", "TypeA", "TargetType", "source-1");
        var owner = Entity("owner-1", "OwnerType", new JObject
        {
            ["Parent"] = Reference("SRC1", "TypeA", "TargetType", "source-1")
        });
        UpsertDataHubEntitiesCommand? capturedUpsert = null;
        var mediator = new RecordingMediator(request => request switch
        {
            GetCosmosDocumentsQuery<ResolutionPromise> => new PagedResults<ResolutionPromise> { Results = [promise] },
            GetDataHubEntitiesByIdRequest => new GetDataHubEntitiesByIdResponse { Results = [owner] },
            FindMatchingEntitiesQuery find => FoundEntity(LookupValue(find), find.EntityType),
            AddTrackedEntityChangeSetsRequest => throw new InvalidOperationException("Tracking should be skipped."),
            UpsertDataHubEntitiesCommand upsert => Capture(upsert, ref capturedUpsert, UpsertSuccess(upsert)),
            DeleteCosmosDocumentsCommand<ResolutionPromise> delete => new DeleteCosmosDocumentsResponse<ResolutionPromise> { Successes = delete.Documents, Failures = [] },
            _ => throw new InvalidOperationException($"Unexpected mediator request {request.GetType().Name}")
        });

        await new ResolveResolutionPromisesRequestHandler(mediator).HandleAsync(new ResolveResolutionPromisesRequest
        {
            PromiseIds = ["promise-1"],
            DoNotTrack = true
        }, CancellationToken.None);

        capturedUpsert.Should().NotBeNull();
        AssertResolvedReference(capturedUpsert!.Entities.Single(), "Parent", "TargetType", "dh-source-1");
        mediator.Requests.Should().NotContain(request => request is AddTrackedEntityChangeSetsRequest);
        mediator.Requests.Should().NotContain(request => request is ProcessPatchEntitiesRequest);
    }

    [Fact]
    public async Task ResolveResolutionPromises_cli_handler_should_process_one_where_page_and_return_continuation()
    {
        var page1 = new List<ResolutionPromise>
        {
            Promise("promise-1", "OwnerType", "owner-1", "Parent", "SRC1", "TypeA", "TargetType", "source-1")
        };
        var owners = new Dictionary<string, JObject>
        {
            ["owner-1"] = Entity("owner-1", "OwnerType", new JObject { ["Parent"] = Reference("SRC1", "TypeA", "TargetType", "source-1") })
        };

        var mediator = new RecordingMediator(request => request switch
        {
            GetCosmosDocumentsQuery<ResolutionPromise> => new PagedResults<ResolutionPromise> { Results = page1, MoreResultsAvailable = true, ContinuationToken = "page-2" },
            GetDataHubEntitiesByIdRequest getOwners => new GetDataHubEntitiesByIdResponse
            {
                Results = getOwners.EntityIds.Select(entityId => owners[entityId]).ToList()
            },
            FindMatchingEntitiesQuery find => FoundEntity(LookupValue(find), find.EntityType),
            AddTrackedEntityChangeSetsRequest => TrackingSuccess(),
            UpsertDataHubEntitiesCommand upsert => UpsertSuccess(upsert),
            DeleteCosmosDocumentsCommand<ResolutionPromise> delete => new DeleteCosmosDocumentsResponse<ResolutionPromise> { Successes = delete.Documents, Failures = [] },
            _ => throw new InvalidOperationException($"Unexpected mediator request {request.GetType().Name}")
        });

        var response = await new ResolveResolutionPromisesRequestHandler(mediator).HandleAsync(new ResolveResolutionPromisesRequest
        {
            WhereClause = "x.DataHubEntityType = @ownerType",
            Parameters = [new DataHubQueryParameter { Name = "ownerType", Value = "OwnerType" }],
            PageSize = 1
        }, CancellationToken.None);

        response.MatchedCount.Should().Be(1);
        response.ResolvedCount.Should().Be(1);
        response.MoreResultsAvailable.Should().BeTrue();
        response.ContinuationToken.Should().Be("page-2");

        var promiseQueries = mediator.Requests.OfType<GetCosmosDocumentsQuery<ResolutionPromise>>().ToList();
        promiseQueries.Should().ContainSingle();
        promiseQueries.Select(query => query.WhereClause).Should().OnlyContain(whereClause => whereClause == "x.DataHubEntityType = @ownerType");
        promiseQueries.SelectMany(query => query.Parameters).Should().OnlyContain(parameter => parameter.Name == "ownerType" && Equals(parameter.Value, "OwnerType"));
        promiseQueries.Select(query => query.PageSize).Should().OnlyContain(pageSize => pageSize == 1);
        promiseQueries.Select(query => query.OrderBy).Should().OnlyContain(orderBy => orderBy == "x.id");
        promiseQueries[0].ContinuationToken.Should().BeNullOrEmpty();

        mediator.Requests.OfType<GetDataHubEntitiesByIdRequest>().Should().ContainSingle();
        mediator.Requests.OfType<FindMatchingEntitiesQuery>().Should().ContainSingle();
        mediator.Requests.OfType<AddTrackedEntityChangeSetsRequest>().Should().ContainSingle();
        mediator.Requests.OfType<UpsertDataHubEntitiesCommand>().Should().ContainSingle();
        mediator.Requests.OfType<DeleteCosmosDocumentsCommand<ResolutionPromise>>().Should().ContainSingle();
        mediator.Requests.Should().NotContain(request => request is ProcessPatchEntitiesRequest);
    }

    [Theory]
    [InlineData(5000, 500)]
    [InlineData(0, 1)]
    public async Task ResolveResolutionPromises_cli_handler_should_bound_requested_page_size(int requestedPageSize, int expectedPageSize)
    {
        GetCosmosDocumentsQuery<ResolutionPromise>? capturedQuery = null;
        var mediator = new RecordingMediator(request => request switch
        {
            GetCosmosDocumentsQuery<ResolutionPromise> query => Capture(query, ref capturedQuery, new PagedResults<ResolutionPromise> { Results = [] }),
            _ => throw new InvalidOperationException($"Unexpected mediator request {request.GetType().Name}")
        });

        await new ResolveResolutionPromisesRequestHandler(mediator).HandleAsync(new ResolveResolutionPromisesRequest
        {
            WhereClause = "x.DataHubEntityType = 'OwnerType'",
            PageSize = requestedPageSize,
            DryRun = true
        }, CancellationToken.None);

        capturedQuery.Should().NotBeNull();
        capturedQuery!.PageSize.Should().Be(expectedPageSize);
    }

    [Fact]
    public async Task ListResolutionPromises_cli_handler_should_query_promises_by_where()
    {
        GetCosmosDocumentsQuery<ResolutionPromise>? capturedQuery = null;
        var promise = Promise("promise-1", "OwnerType", "owner-1", "Parent", "SRC1", "TypeA", "TargetType", "source-1");
        var mediator = new RecordingMediator(request => request switch
        {
            GetCosmosDocumentsQuery<ResolutionPromise> query => Capture(query, ref capturedQuery, new PagedResults<ResolutionPromise>
            {
                Results = [promise],
                ContinuationToken = "page-2",
                MoreResultsAvailable = true,
                ResultCount = 1
            }),
            _ => throw new InvalidOperationException($"Unexpected mediator request {request.GetType().Name}")
        });

        var response = await new ListResolutionPromisesRequestHandler(mediator).HandleAsync(new ListResolutionPromisesRequest
        {
            WhereClause = "x.DataHubEntityType = @ownerType",
            Parameters = [new DataHubQueryParameter { Name = "ownerType", Value = "OwnerType" }],
            PageSize = 25
        }, CancellationToken.None);

        capturedQuery.Should().NotBeNull();
        capturedQuery!.WhereClause.Should().Be("x.DataHubEntityType = @ownerType");
        capturedQuery.Parameters.Should().ContainSingle(parameter => parameter.Name == "ownerType" && Equals(parameter.Value, "OwnerType"));
        capturedQuery.PageSize.Should().Be(25);
        response.Results.Should().ContainSingle().Which.PromiseId.Should().Be("promise-1");
        response.MoreResultsAvailable.Should().BeTrue();
        response.ContinuationToken.Should().Be("page-2");
    }

    [Fact]
    public async Task GetResolutionPromises_cli_handler_should_query_promises_by_ids()
    {
        GetCosmosDocumentsQuery<ResolutionPromise>? capturedQuery = null;
        var mediator = new RecordingMediator(request => request switch
        {
            GetCosmosDocumentsQuery<ResolutionPromise> query => Capture(query, ref capturedQuery, new PagedResults<ResolutionPromise>
            {
                Results = [Promise("promise-1", "OwnerType", "owner-1", "Parent", "SRC1", "TypeA", "TargetType", "source-1")]
            }),
            _ => throw new InvalidOperationException($"Unexpected mediator request {request.GetType().Name}")
        });

        var response = await new GetResolutionPromisesRequestHandler(mediator).HandleAsync(new GetResolutionPromisesRequest
        {
            PromiseIds = ["promise-1", "promise-2"]
        }, CancellationToken.None);

        capturedQuery.Should().NotBeNull();
        capturedQuery!.WhereClause.Should().Be("x.id in (@promiseId0,@promiseId1)");
        capturedQuery.Parameters.Should().HaveCount(2);
        response.Results.Should().ContainSingle().Which.PromiseId.Should().Be("promise-1");
    }

    [Fact]
    public async Task DeleteResolutionPromises_cli_handler_should_delete_promises_by_ids()
    {
        var promise = Promise("promise-1", "OwnerType", "owner-1", "Parent", "SRC1", "TypeA", "TargetType", "source-1");
        DeleteCosmosDocumentsCommand<ResolutionPromise>? capturedDelete = null;
        var mediator = new RecordingMediator(request => request switch
        {
            GetCosmosDocumentsQuery<ResolutionPromise> => new PagedResults<ResolutionPromise> { Results = [promise] },
            DeleteCosmosDocumentsCommand<ResolutionPromise> delete => CaptureDelete(delete, ref capturedDelete),
            _ => throw new InvalidOperationException($"Unexpected mediator request {request.GetType().Name}")
        });

        var response = await new DeleteResolutionPromisesRequestHandler(mediator).HandleAsync(new DeleteResolutionPromisesRequest
        {
            PromiseIds = ["promise-1"]
        }, CancellationToken.None);

        response.MatchedCount.Should().Be(1);
        response.DeletedCount.Should().Be(1);
        response.Results.Should().ContainSingle().Which.Status.Should().Be("Deleted");
        capturedDelete.Should().NotBeNull();
        capturedDelete!.Documents.Should().ContainSingle().Which.id.Should().Be("promise-1");
    }

    [Fact]
    public async Task DeleteResolutionPromises_cli_handler_should_treat_not_found_delete_result_as_deleted()
    {
        var promise = Promise("promise-1", "OwnerType", "owner-1", "Parent", "SRC1", "TypeA", "TargetType", "source-1");
        var mediator = new RecordingMediator(request => request switch
        {
            GetCosmosDocumentsQuery<ResolutionPromise> => new PagedResults<ResolutionPromise> { Results = [promise] },
            DeleteCosmosDocumentsCommand<ResolutionPromise> => new DeleteCosmosDocumentsResponse<ResolutionPromise>
            {
                Successes = [],
                Failures =
                [
                    new DataAccessFailure<ResolutionPromise>(
                        promise,
                        new InvalidOperationException("Response status code does not indicate success: NotFound (404)."))
                ]
            },
            _ => throw new InvalidOperationException($"Unexpected mediator request {request.GetType().Name}")
        });

        var response = await new DeleteResolutionPromisesRequestHandler(mediator).HandleAsync(new DeleteResolutionPromisesRequest
        {
            PromiseIds = ["promise-1"]
        }, CancellationToken.None);

        response.MatchedCount.Should().Be(1);
        response.DeletedCount.Should().Be(1);
        response.FailedCount.Should().Be(0);
        var result = response.Results.Should().ContainSingle().Subject;
        result.PromiseId.Should().Be("promise-1");
        result.Status.Should().Be("Deleted");
        result.Reason.Should().Be("Already deleted.");
    }

    [Fact]
    public async Task DeleteResolutionPromises_cli_handler_should_delete_one_where_page_and_return_continuation()
    {
        GetCosmosDocumentsQuery<ResolutionPromise>? capturedQuery = null;
        var page1 = new List<ResolutionPromise> { Promise("promise-1", "OwnerType", "owner-1", "Parent", "SRC1", "TypeA", "TargetType", "source-1") };
        var mediator = new RecordingMediator(request => request switch
        {
            GetCosmosDocumentsQuery<ResolutionPromise> query => Capture(query, ref capturedQuery, new PagedResults<ResolutionPromise> { Results = page1, MoreResultsAvailable = true, ContinuationToken = "page-2" }),
            DeleteCosmosDocumentsCommand<ResolutionPromise> delete => new DeleteCosmosDocumentsResponse<ResolutionPromise> { Successes = delete.Documents, Failures = [] },
            _ => throw new InvalidOperationException($"Unexpected mediator request {request.GetType().Name}")
        });

        var response = await new DeleteResolutionPromisesRequestHandler(mediator).HandleAsync(new DeleteResolutionPromisesRequest
        {
            WhereClause = "x.DataHubEntityType = @ownerType",
            Parameters = [new DataHubQueryParameter { Name = "ownerType", Value = "OwnerType" }],
            PageSize = 1
        }, CancellationToken.None);

        response.MatchedCount.Should().Be(1);
        response.DeletedCount.Should().Be(1);
        response.MoreResultsAvailable.Should().BeTrue();
        response.ContinuationToken.Should().Be("page-2");
        mediator.Requests.OfType<GetCosmosDocumentsQuery<ResolutionPromise>>().Should().ContainSingle();
        capturedQuery.Should().NotBeNull();
        capturedQuery!.WhereClause.Should().Be("x.DataHubEntityType = @ownerType");
        capturedQuery.Parameters.Should().ContainSingle(parameter => parameter.Name == "ownerType" && Equals(parameter.Value, "OwnerType"));
        capturedQuery.PageSize.Should().Be(1);
        capturedQuery.ContinuationToken.Should().BeNullOrEmpty();
        mediator.Requests.OfType<DeleteCosmosDocumentsCommand<ResolutionPromise>>().Should().ContainSingle();
    }

    [Fact]
    public async Task DeleteResolutionPromises_cli_handler_should_ignore_continuation_token_for_destructive_where_delete()
    {
        GetCosmosDocumentsQuery<ResolutionPromise>? capturedQuery = null;
        var mediator = new RecordingMediator(request => request switch
        {
            GetCosmosDocumentsQuery<ResolutionPromise> query => Capture(query, ref capturedQuery, new PagedResults<ResolutionPromise> { Results = [] }),
            DeleteCosmosDocumentsCommand<ResolutionPromise> => throw new InvalidOperationException("No promises should be deleted."),
            _ => throw new InvalidOperationException($"Unexpected mediator request {request.GetType().Name}")
        });

        var response = await new DeleteResolutionPromisesRequestHandler(mediator).HandleAsync(new DeleteResolutionPromisesRequest
        {
            WhereClause = "x.DataHubEntityType = 'OwnerType'",
            ContinuationToken = "page-2",
            DryRun = false
        }, CancellationToken.None);

        response.MatchedCount.Should().Be(0);
        capturedQuery.Should().NotBeNull();
        capturedQuery!.ContinuationToken.Should().BeNull();
    }

    [Fact]
    public async Task DeleteResolutionPromises_cli_handler_should_preserve_continuation_token_for_dry_run_where_scan()
    {
        GetCosmosDocumentsQuery<ResolutionPromise>? capturedQuery = null;
        var mediator = new RecordingMediator(request => request switch
        {
            GetCosmosDocumentsQuery<ResolutionPromise> query => Capture(query, ref capturedQuery, new PagedResults<ResolutionPromise> { Results = [] }),
            DeleteCosmosDocumentsCommand<ResolutionPromise> => throw new InvalidOperationException("Dry-run should not delete promises."),
            _ => throw new InvalidOperationException($"Unexpected mediator request {request.GetType().Name}")
        });

        var response = await new DeleteResolutionPromisesRequestHandler(mediator).HandleAsync(new DeleteResolutionPromisesRequest
        {
            WhereClause = "x.DataHubEntityType = 'OwnerType'",
            ContinuationToken = "page-2",
            DryRun = true
        }, CancellationToken.None);

        response.MatchedCount.Should().Be(0);
        capturedQuery.Should().NotBeNull();
        capturedQuery!.ContinuationToken.Should().Be("page-2");
    }

    [Fact]
    public async Task DeleteResolutionPromises_cli_handler_should_cap_where_page_size()
    {
        GetCosmosDocumentsQuery<ResolutionPromise>? capturedQuery = null;
        var mediator = new RecordingMediator(request => request switch
        {
            GetCosmosDocumentsQuery<ResolutionPromise> query => Capture(query, ref capturedQuery, new PagedResults<ResolutionPromise> { Results = [] }),
            DeleteCosmosDocumentsCommand<ResolutionPromise> => throw new InvalidOperationException("No promises should be deleted."),
            _ => throw new InvalidOperationException($"Unexpected mediator request {request.GetType().Name}")
        });

        var response = await new DeleteResolutionPromisesRequestHandler(mediator).HandleAsync(new DeleteResolutionPromisesRequest
        {
            WhereClause = "x.DataHubEntityType = 'OwnerType'",
            PageSize = 1000
        }, CancellationToken.None);

        response.MatchedCount.Should().Be(0);
        capturedQuery.Should().NotBeNull();
        capturedQuery!.PageSize.Should().Be(500);
    }

    [Fact]
    public async Task DeleteResolutionPromises_cli_handler_should_delete_loaded_promises_in_batches()
    {
        var promises = Enumerable.Range(1, 1250)
            .Select(index => Promise($"promise-{index}", "OwnerType", "owner-1", "Parent", "SRC1", "TypeA", "TargetType", $"source-{index}"))
            .ToList();
        var mediator = new RecordingMediator(request => request switch
        {
            GetCosmosDocumentsQuery<ResolutionPromise> => new PagedResults<ResolutionPromise> { Results = promises },
            DeleteCosmosDocumentsCommand<ResolutionPromise> delete => new DeleteCosmosDocumentsResponse<ResolutionPromise> { Successes = delete.Documents, Failures = [] },
            _ => throw new InvalidOperationException($"Unexpected mediator request {request.GetType().Name}")
        });

        var response = await new DeleteResolutionPromisesRequestHandler(mediator).HandleAsync(new DeleteResolutionPromisesRequest
        {
            PromiseIds = promises.Select(promise => promise.id).ToList(),
            PageSize = 1250
        }, CancellationToken.None);

        response.MatchedCount.Should().Be(1250);
        response.DeletedCount.Should().Be(1250);
        mediator.Requests.OfType<DeleteCosmosDocumentsCommand<ResolutionPromise>>()
            .Select(command => command.Documents.Count)
            .Should().Equal(500, 500, 250);
    }

    [Fact]
    public async Task DeleteResolutionPromises_cli_handler_should_ignore_null_promises_in_page_and_delete_results()
    {
        var promise = Promise("promise-1", "OwnerType", "owner-1", "Parent", "SRC1", "TypeA", "TargetType", "source-1");
        var mediator = new RecordingMediator(request => request switch
        {
            GetCosmosDocumentsQuery<ResolutionPromise> => new PagedResults<ResolutionPromise>
            {
                Results = [null!, promise]
            },
            DeleteCosmosDocumentsCommand<ResolutionPromise> => new DeleteCosmosDocumentsResponse<ResolutionPromise>
            {
                Successes = [null!, promise],
                Failures = [null!, new DataAccessFailure<ResolutionPromise>(null!, new InvalidOperationException("Missing promise."))]
            },
            _ => throw new InvalidOperationException($"Unexpected mediator request {request.GetType().Name}")
        });

        var response = await new DeleteResolutionPromisesRequestHandler(mediator).HandleAsync(new DeleteResolutionPromisesRequest
        {
            WhereClause = "x.DataHubEntityType = 'OwnerType'"
        }, CancellationToken.None);

        response.MatchedCount.Should().Be(1);
        response.DeletedCount.Should().Be(1);
        response.FailedCount.Should().Be(0);
        response.Results.Should().ContainSingle().Which.PromiseId.Should().Be("promise-1");
        mediator.Requests.OfType<DeleteCosmosDocumentsCommand<ResolutionPromise>>()
            .Should().ContainSingle()
            .Which.Documents.Should().ContainSingle().Which.id.Should().Be("promise-1");
    }

    [Fact]
    public async Task DeleteResolutionPromises_cli_handler_should_report_dry_run_without_deleting()
    {
        var mediator = new RecordingMediator(request => request switch
        {
            GetCosmosDocumentsQuery<ResolutionPromise> => new PagedResults<ResolutionPromise>
            {
                Results = [Promise("promise-1", "OwnerType", "owner-1", "Parent", "SRC1", "TypeA", "TargetType", "source-1")]
            },
            DeleteCosmosDocumentsCommand<ResolutionPromise> => throw new InvalidOperationException("Dry-run should not delete promises."),
            _ => throw new InvalidOperationException($"Unexpected mediator request {request.GetType().Name}")
        });

        var response = await new DeleteResolutionPromisesRequestHandler(mediator).HandleAsync(new DeleteResolutionPromisesRequest
        {
            WhereClause = "x.DataHubEntityType = 'OwnerType'",
            DryRun = true
        }, CancellationToken.None);

        response.MatchedCount.Should().Be(1);
        response.DeletedCount.Should().Be(0);
        response.Results.Should().ContainSingle().Which.Status.Should().Be("DryRun");
        mediator.Requests.Should().NotContain(request => request is DeleteCosmosDocumentsCommand<ResolutionPromise>);
    }

    [Fact]
    public async Task PatchResolutionPromise_cli_handler_should_patch_promise_by_id()
    {
        var promise = Promise("promise-1", "OwnerType", "owner-1", "Parent", "SRC1", "TypeA", "TargetType", "source-1");
        UpsertCosmosDocumentsCommand<ResolutionPromise>? capturedUpsert = null;
        var mediator = new RecordingMediator(request => request switch
        {
            GetCosmosDocumentsQuery<ResolutionPromise> => new PagedResults<ResolutionPromise> { Results = [promise] },
            UpsertCosmosDocumentsCommand<ResolutionPromise> upsert => Capture(upsert, ref capturedUpsert, new UpsertCosmosDocumentsResponse<ResolutionPromise>
            {
                Successes = upsert.Documents,
                Failures = []
            }),
            _ => throw new InvalidOperationException($"Unexpected mediator request {request.GetType().Name}")
        });

        var response = await new PatchResolutionPromiseRequestHandler(mediator).HandleAsync(new PatchResolutionPromiseRequest
        {
            PromiseId = "promise-1",
            Operations =
            [
                new Patch
                {
                    Operation = "set",
                    Path = "ExternalEntityReference.EntityId",
                    Value = "source-2"
                }
            ]
        }, CancellationToken.None);

        response.Success.Should().BeTrue();
        response.Changed.Should().BeTrue();
        response.Result.SourceEntityId.Should().Be("source-2");
        capturedUpsert.Should().NotBeNull();
        capturedUpsert!.Documents.Should().ContainSingle().Which.ExternalEntityReference.EntityId.Should().Be("source-2");
    }

    [Fact]
    public async Task PatchResolutionPromise_cli_handler_should_report_dry_run_without_upserting()
    {
        var promise = Promise("promise-1", "OwnerType", "owner-1", "Parent", "SRC1", "TypeA", "TargetType", "source-1");
        var mediator = new RecordingMediator(request => request switch
        {
            GetCosmosDocumentsQuery<ResolutionPromise> => new PagedResults<ResolutionPromise> { Results = [promise] },
            UpsertCosmosDocumentsCommand<ResolutionPromise> => throw new InvalidOperationException("Dry-run should not upsert promises."),
            _ => throw new InvalidOperationException($"Unexpected mediator request {request.GetType().Name}")
        });

        var response = await new PatchResolutionPromiseRequestHandler(mediator).HandleAsync(new PatchResolutionPromiseRequest
        {
            PromiseId = "promise-1",
            DryRun = true,
            Operations =
            [
                new Patch
                {
                    Operation = "set",
                    Path = "EntityReferencePath",
                    Value = "UpdatedPath"
                }
            ]
        }, CancellationToken.None);

        response.Success.Should().BeTrue();
        response.Changed.Should().BeTrue();
        response.Status.Should().Be("DryRun");
        response.Result.EntityReferencePath.Should().Be("UpdatedPath");
        mediator.Requests.Should().NotContain(request => request is UpsertCosmosDocumentsCommand<ResolutionPromise>);
    }

    [Fact]
    public async Task ResolveEntityReferenceResolutionPromises_should_apply_multiple_resolved_promises_to_one_owner_entity()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(ResolveEntityReferenceResolutionPromises_should_apply_multiple_resolved_promises_to_one_owner_entity)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(ResolveEntityReferenceResolutionPromises_should_apply_multiple_resolved_promises_to_one_owner_entity))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    UpsertDataHubEntitiesCommand? capturedUpsert = null;
                    DeleteCosmosDocumentsCommand<ResolutionPromise>? capturedDelete = null;
                    AddTrackedEntityChangeSetsRequest? capturedTracking = null;
                    var promises = new List<ResolutionPromise>
                    {
                        Promise("promise-primary", "OwnerType", "owner-1", "Primary", "SRC1", "TypeA", "ParentDH", "parent-1"),
                        Promise("promise-child", "OwnerType", "owner-1", "Children[0].Reference", "SRC1", "TypeB", "ChildDH", "child-1")
                    };
                    var owner = Entity("owner-1", "OwnerType", new JObject
                    {
                        ["Primary"] = Reference("SRC1", "TypeA", "ParentDH", "parent-1"),
                        ["Children"] = new JArray
                        {
                            new JObject
                            {
                                ["Reference"] = Reference("SRC1", "TypeB", "ChildDH", "child-1")
                            }
                        }
                    });
                    var mediator = new RecordingMediator(request =>
                    {
                        switch (request)
                        {
                            case GetCosmosDocumentsQuery<ResolutionPromise>:
                                return new PagedResults<ResolutionPromise> { Results = promises };
                            case GetDataHubEntitiesByIdRequest:
                                return new GetDataHubEntitiesByIdResponse { Results = [owner] };
                            case AddTrackedEntityChangeSetsRequest addTracking:
                                capturedTracking = addTracking;
                                return new AddTrackedEntityChangeSetsResponse { Successes = [], Failures = [] };
                            case UpsertDataHubEntitiesCommand upsert:
                                capturedUpsert = upsert;
                                return new UpsertDataHubEntitiesResponse { Successes = upsert.Entities, Failures = [] };
                            case DeleteCosmosDocumentsCommand<ResolutionPromise> delete:
                                capturedDelete = delete;
                                return new DeleteCosmosDocumentsResponse<ResolutionPromise> { Successes = delete.Documents, Failures = [] };
                            default:
                                throw new InvalidOperationException(request.GetType().FullName);
                        }
                    });
                    var handler = new ResolveEntityReferenceResolutionPromisesRequestHandler(mediator, new FixedTimeService(DateTimeOffset.Parse("2024-12-01T00:00:00Z")));

                    var response = await handler.HandleAsync(new ResolveEntityReferenceResolutionPromisesRequest
                    {
                        SourceSystemEntityIds = ["parent-1", "child-1"],
                        ResolvedReferencedEntities =
                        [
                            Resolved("SRC1", "TypeA", "parent-1", "ParentDH", "dh-parent-1"),
                            Resolved("SRC1", "TypeB", "child-1", "ChildDH", "dh-child-1")
                        ]
                    }, CancellationToken.None);

                    var updated = response.UpdatedDataHubEntities.Should().ContainSingle().Subject;
                    AssertResolvedReference(updated, "Primary", "ParentDH", "dh-parent-1");
                    AssertResolvedReference(updated, "Children[0].Reference", "ChildDH", "dh-child-1");
                    capturedUpsert.Should().NotBeNull();
                    capturedUpsert!.Entities.Should().ContainSingle();
                    capturedDelete.Should().NotBeNull();
                    capturedDelete!.Documents.Should().BeEquivalentTo(promises);
                    capturedTracking.Should().NotBeNull();
                    capturedTracking!.Requests.Should().HaveCount(2);

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
    public async Task ResolveEntityReferenceResolutionPromises_should_match_by_data_source_and_leave_cross_source_promises_unresolved()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(ResolveEntityReferenceResolutionPromises_should_match_by_data_source_and_leave_cross_source_promises_unresolved)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(ResolveEntityReferenceResolutionPromises_should_match_by_data_source_and_leave_cross_source_promises_unresolved))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    UpsertDataHubEntitiesCommand? capturedUpsert = null;
                    DeleteCosmosDocumentsCommand<ResolutionPromise>? capturedDelete = null;
                    var promises = new List<ResolutionPromise>
                    {
                        Promise("promise-src1", "OwnerType", "owner-1", "Src1Parent", "SRC1", "TypeA", "ParentDH", "same-source-id"),
                        Promise("promise-src2", "OwnerType", "owner-1", "Src2Parent", "SRC2", "TypeA", "ParentDH", "same-source-id")
                    };
                    var owner = Entity("owner-1", "OwnerType", new JObject
                    {
                        ["Src1Parent"] = Reference("SRC1", "TypeA", "ParentDH", "same-source-id"),
                        ["Src2Parent"] = Reference("SRC2", "TypeA", "ParentDH", "same-source-id")
                    });
                    var mediator = new RecordingMediator(request =>
                    {
                        switch (request)
                        {
                            case GetCosmosDocumentsQuery<ResolutionPromise>:
                                return new PagedResults<ResolutionPromise> { Results = promises };
                            case GetDataHubEntitiesByIdRequest:
                                return new GetDataHubEntitiesByIdResponse { Results = [owner] };
                            case AddTrackedEntityChangeSetsRequest addTracking:
                                return new AddTrackedEntityChangeSetsResponse { Successes = addTracking.Requests.Select(r => new ChangeTrackingEntry()).ToList(), Failures = [] };
                            case UpsertDataHubEntitiesCommand upsert:
                                capturedUpsert = upsert;
                                return new UpsertDataHubEntitiesResponse { Successes = upsert.Entities, Failures = [] };
                            case DeleteCosmosDocumentsCommand<ResolutionPromise> delete:
                                capturedDelete = delete;
                                return new DeleteCosmosDocumentsResponse<ResolutionPromise> { Successes = delete.Documents, Failures = [] };
                            default:
                                throw new InvalidOperationException(request.GetType().FullName);
                        }
                    });
                    var handler = new ResolveEntityReferenceResolutionPromisesRequestHandler(mediator, new FixedTimeService(DateTimeOffset.Parse("2024-12-01T00:00:00Z")));

                    var response = await handler.HandleAsync(new ResolveEntityReferenceResolutionPromisesRequest
                    {
                        SourceSystemEntityIds = ["same-source-id"],
                        ResolvedReferencedEntities = [Resolved("SRC1", "TypeA", "same-source-id", "ParentDH", "dh-src1-parent")]
                    }, CancellationToken.None);

                    var updated = response.UpdatedDataHubEntities.Should().ContainSingle().Subject;
                    AssertResolvedReference(updated, "Src1Parent", "ParentDH", "dh-src1-parent");
                    AssertExternalReference(updated, "Src2Parent", "SRC2", "TypeA", "ParentDH", "same-source-id");
                    capturedUpsert.Should().NotBeNull();
                    capturedUpsert!.Entities.Should().ContainSingle();
                    capturedDelete.Should().NotBeNull();
                    capturedDelete!.Documents.Should().ContainSingle().Which.id.Should().Be("promise-src1");

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
    public async Task ResolveEntityReferenceResolutionPromises_should_apply_same_owner_promises_across_pages_once()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(ResolveEntityReferenceResolutionPromises_should_apply_same_owner_promises_across_pages_once)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(ResolveEntityReferenceResolutionPromises_should_apply_same_owner_promises_across_pages_once))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    UpsertDataHubEntitiesCommand? capturedUpsert = null;
                    DeleteCosmosDocumentsCommand<ResolutionPromise>? capturedDelete = null;
                    var page1 = new List<ResolutionPromise>
                    {
                        Promise("promise-primary", "OwnerType", "owner-1", "Primary", "SRC1", "TypeA", "ParentDH", "parent-1")
                    };
                    var page2 = new List<ResolutionPromise>
                    {
                        Promise("promise-secondary", "OwnerType", "owner-1", "Secondary", "SRC1", "TypeB", "ChildDH", "child-1")
                    };
                    var owner = Entity("owner-1", "OwnerType", new JObject
                    {
                        ["Primary"] = Reference("SRC1", "TypeA", "ParentDH", "parent-1"),
                        ["Secondary"] = Reference("SRC1", "TypeB", "ChildDH", "child-1")
                    });
                    var mediator = new RecordingMediator(request =>
                    {
                        switch (request)
                        {
                            case GetCosmosDocumentsQuery<ResolutionPromise> query:
                                return string.IsNullOrEmpty(query.ContinuationToken)
                                    ? new PagedResults<ResolutionPromise> { Results = page1, MoreResultsAvailable = true, ContinuationToken = "page-2" }
                                    : new PagedResults<ResolutionPromise> { Results = page2, MoreResultsAvailable = false };
                            case GetDataHubEntitiesByIdRequest:
                                return new GetDataHubEntitiesByIdResponse { Results = [owner] };
                            case AddTrackedEntityChangeSetsRequest addTracking:
                                return new AddTrackedEntityChangeSetsResponse { Successes = addTracking.Requests.Select(r => new ChangeTrackingEntry()).ToList(), Failures = [] };
                            case UpsertDataHubEntitiesCommand upsert:
                                capturedUpsert = upsert;
                                return new UpsertDataHubEntitiesResponse { Successes = upsert.Entities, Failures = [] };
                            case DeleteCosmosDocumentsCommand<ResolutionPromise> delete:
                                capturedDelete = delete;
                                return new DeleteCosmosDocumentsResponse<ResolutionPromise> { Successes = delete.Documents, Failures = [] };
                            default:
                                throw new InvalidOperationException(request.GetType().FullName);
                        }
                    });
                    var handler = new ResolveEntityReferenceResolutionPromisesRequestHandler(mediator, new FixedTimeService(DateTimeOffset.Parse("2024-12-01T00:00:00Z")));

                    var response = await handler.HandleAsync(new ResolveEntityReferenceResolutionPromisesRequest
                    {
                        SourceSystemEntityIds = ["parent-1", "child-1"],
                        ResolvedReferencedEntities =
                        [
                            Resolved("SRC1", "TypeA", "parent-1", "ParentDH", "dh-parent-1"),
                            Resolved("SRC1", "TypeB", "child-1", "ChildDH", "dh-child-1")
                        ]
                    }, CancellationToken.None);

                    var updated = response.UpdatedDataHubEntities.Should().ContainSingle().Subject;
                    AssertResolvedReference(updated, "Primary", "ParentDH", "dh-parent-1");
                    AssertResolvedReference(updated, "Secondary", "ChildDH", "dh-child-1");
                    capturedUpsert.Should().NotBeNull();
                    capturedUpsert!.Entities.Should().ContainSingle();
                    capturedDelete.Should().NotBeNull();
                    capturedDelete!.Documents.Should().HaveCount(2);

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
    public async Task ResolveEntityReferenceResolutionPromises_should_handle_multiple_owner_entity_types_without_cross_type_leakage()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(ResolveEntityReferenceResolutionPromises_should_handle_multiple_owner_entity_types_without_cross_type_leakage)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(ResolveEntityReferenceResolutionPromises_should_handle_multiple_owner_entity_types_without_cross_type_leakage))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var promises = new List<ResolutionPromise>
                    {
                        Promise("promise-a", "OwnerTypeA", "owner-a", "Parent", "SRC1", "TypeA", "ParentDH", "parent-a"),
                        Promise("promise-b", "OwnerTypeB", "owner-b", "Parent", "SRC1", "TypeB", "ChildDH", "parent-b")
                    };
                    var ownersByType = new Dictionary<string, List<JObject>>
                    {
                        ["OwnerTypeA"] = [Entity("owner-a", "OwnerTypeA", new JObject { ["Parent"] = Reference("SRC1", "TypeA", "ParentDH", "parent-a") })],
                        ["OwnerTypeB"] = [Entity("owner-b", "OwnerTypeB", new JObject { ["Parent"] = Reference("SRC1", "TypeB", "ChildDH", "parent-b") })]
                    };
                    var mediator = new RecordingMediator(request =>
                    {
                        return request switch
                        {
                            GetCosmosDocumentsQuery<ResolutionPromise> => new PagedResults<ResolutionPromise> { Results = promises },
                            GetDataHubEntitiesByIdRequest getOwners => new GetDataHubEntitiesByIdResponse { Results = ownersByType[getOwners.EntityType] },
                            AddTrackedEntityChangeSetsRequest addTracking => new AddTrackedEntityChangeSetsResponse { Successes = addTracking.Requests.Select(r => new ChangeTrackingEntry()).ToList(), Failures = [] },
                            UpsertDataHubEntitiesCommand upsert => new UpsertDataHubEntitiesResponse { Successes = upsert.Entities, Failures = [] },
                            DeleteCosmosDocumentsCommand<ResolutionPromise> delete => new DeleteCosmosDocumentsResponse<ResolutionPromise> { Successes = delete.Documents, Failures = [] },
                            _ => throw new InvalidOperationException(request.GetType().FullName)
                        };
                    });
                    var handler = new ResolveEntityReferenceResolutionPromisesRequestHandler(mediator, new FixedTimeService(DateTimeOffset.Parse("2024-12-01T00:00:00Z")));

                    var response = await handler.HandleAsync(new ResolveEntityReferenceResolutionPromisesRequest
                    {
                        SourceSystemEntityIds = ["parent-a", "parent-b"],
                        ResolvedReferencedEntities =
                        [
                            Resolved("SRC1", "TypeA", "parent-a", "ParentDH", "dh-parent-a"),
                            Resolved("SRC1", "TypeB", "parent-b", "ChildDH", "dh-parent-b")
                        ]
                    }, CancellationToken.None);

                    response.UpdatedDataHubEntities.Should().HaveCount(2);
                    AssertResolvedReference(response.UpdatedDataHubEntities.Single(e => e.Value<string>(nameof(DataHubEntity.entityType)) == "OwnerTypeA"), "Parent", "ParentDH", "dh-parent-a");
                    AssertResolvedReference(response.UpdatedDataHubEntities.Single(e => e.Value<string>(nameof(DataHubEntity.entityType)) == "OwnerTypeB"), "Parent", "ChildDH", "dh-parent-b");
                    mediator.Requests.OfType<GetDataHubEntitiesByIdRequest>().Select(r => r.EntityType)
                        .Should()
                        .BeEquivalentTo("OwnerTypeA", "OwnerTypeB");

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
    public async Task ResolveEntityReferenceResolutionPromises_should_use_owner_last_updated_when_reference_timestamp_is_missing()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(ResolveEntityReferenceResolutionPromises_should_use_owner_last_updated_when_reference_timestamp_is_missing)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(ResolveEntityReferenceResolutionPromises_should_use_owner_last_updated_when_reference_timestamp_is_missing))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    AddTrackedEntityChangeSetsRequest? capturedTracking = null;
                    var ownerLastUpdated = DateTimeOffset.Parse("2024-03-01T00:00:00Z");
                    var mediator = new RecordingMediator(request =>
                    {
                        switch (request)
                        {
                            case GetCosmosDocumentsQuery<ResolutionPromise>:
                                return new PagedResults<ResolutionPromise>
                                {
                                    Results = [Promise("promise-1", "OwnerType", "owner-1", "Parent", "SRC1", "TypeA", "ParentDH", "parent-1")]
                                };
                            case GetDataHubEntitiesByIdRequest:
                                return new GetDataHubEntitiesByIdResponse
                                {
                                    Results = [Entity("owner-1", "OwnerType", new JObject { ["Parent"] = Reference("SRC1", "TypeA", "ParentDH", "parent-1") }, ownerLastUpdated)]
                                };
                            case AddTrackedEntityChangeSetsRequest addTracking:
                                capturedTracking = addTracking;
                                return new AddTrackedEntityChangeSetsResponse { Successes = [], Failures = [] };
                            case UpsertDataHubEntitiesCommand upsert:
                                return new UpsertDataHubEntitiesResponse { Successes = upsert.Entities, Failures = [] };
                            case DeleteCosmosDocumentsCommand<ResolutionPromise> delete:
                                return new DeleteCosmosDocumentsResponse<ResolutionPromise> { Successes = delete.Documents, Failures = [] };
                            default:
                                throw new InvalidOperationException(request.GetType().FullName);
                        }
                    });
                    var handler = new ResolveEntityReferenceResolutionPromisesRequestHandler(mediator, new FixedTimeService(DateTimeOffset.Parse("2024-12-01T00:00:00Z")));

                    await handler.HandleAsync(new ResolveEntityReferenceResolutionPromisesRequest
                    {
                        SourceSystemEntityIds = ["parent-1"],
                        ResolvedReferencedEntities = [Resolved("SRC1", "TypeA", "parent-1", "ParentDH", "dh-parent-1")]
                    }, CancellationToken.None);

                    capturedTracking.Should().NotBeNull();
                    capturedTracking!.Requests.Should().ContainSingle().Which.TimeStamp.Should().Be(ownerLastUpdated);

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
    public async Task ResolveEntityReferenceResolutionPromises_should_delete_promises_when_owner_entity_is_missing()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(ResolveEntityReferenceResolutionPromises_should_delete_promises_when_owner_entity_is_missing)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(ResolveEntityReferenceResolutionPromises_should_delete_promises_when_owner_entity_is_missing))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    DeleteCosmosDocumentsCommand<ResolutionPromise>? capturedDelete = null;
                    var mediator = new RecordingMediator(request =>
                    {
                        return request switch
                        {
                            GetCosmosDocumentsQuery<ResolutionPromise> => new PagedResults<ResolutionPromise>
                            {
                                Results = [Promise("promise-1", "OwnerType", "missing-owner", "Parent", "SRC1", "TypeA", "ParentDH", "parent-1")]
                            },
                            GetDataHubEntitiesByIdRequest => new GetDataHubEntitiesByIdResponse { Results = [] },
                            AddTrackedEntityChangeSetsRequest => throw new InvalidOperationException("Missing owner should not be tracked."),
                            UpsertDataHubEntitiesCommand => throw new InvalidOperationException("Missing owner should not be upserted."),
                            DeleteCosmosDocumentsCommand<ResolutionPromise> delete => CaptureDelete(delete, ref capturedDelete),
                            _ => throw new InvalidOperationException(request.GetType().FullName)
                        };
                    });
                    var handler = new ResolveEntityReferenceResolutionPromisesRequestHandler(mediator, new FixedTimeService(DateTimeOffset.Parse("2024-12-01T00:00:00Z")));

                    var response = await handler.HandleAsync(new ResolveEntityReferenceResolutionPromisesRequest
                    {
                        SourceSystemEntityIds = ["parent-1"],
                        ResolvedReferencedEntities = [Resolved("SRC1", "TypeA", "parent-1", "ParentDH", "dh-parent-1")]
                    }, CancellationToken.None);

                    response.UpdatedDataHubEntities.Should().BeEmpty();
                    capturedDelete.Should().NotBeNull();
                    capturedDelete!.Documents.Should().ContainSingle().Which.id.Should().Be("promise-1");

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
    public async Task ResolveEntityReferenceResolutionPromises_should_fail_when_promise_path_is_missing()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(ResolveEntityReferenceResolutionPromises_should_fail_when_promise_path_is_missing)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(ResolveEntityReferenceResolutionPromises_should_fail_when_promise_path_is_missing))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var mediator = new RecordingMediator(request =>
                    {
                        return request switch
                        {
                            GetCosmosDocumentsQuery<ResolutionPromise> => new PagedResults<ResolutionPromise>
                            {
                                Results = [Promise("promise-1", "OwnerType", "owner-1", "MissingPath", "SRC1", "TypeA", "ParentDH", "parent-1")]
                            },
                            GetDataHubEntitiesByIdRequest => new GetDataHubEntitiesByIdResponse
                            {
                                Results = [Entity("owner-1", "OwnerType", new JObject { ["Parent"] = Reference("SRC1", "TypeA", "ParentDH", "parent-1") })]
                            },
                            _ => throw new InvalidOperationException(request.GetType().FullName)
                        };
                    });
                    var handler = new ResolveEntityReferenceResolutionPromisesRequestHandler(mediator, new FixedTimeService(DateTimeOffset.Parse("2024-12-01T00:00:00Z")));

                    var act = () => handler.HandleAsync(new ResolveEntityReferenceResolutionPromisesRequest
                    {
                        SourceSystemEntityIds = ["parent-1"],
                        ResolvedReferencedEntities = [Resolved("SRC1", "TypeA", "parent-1", "ParentDH", "dh-parent-1")]
                    }, CancellationToken.None);

                    await act.Should().ThrowAsync<Exception>().WithMessage("Entity reference to resolve not found");

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
    public async Task Validators_should_require_non_empty_inputs()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(Validators_should_require_non_empty_inputs)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(Validators_should_require_non_empty_inputs))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    new CreateDeferredEntityResolutionPromisesRequestValidator()
                        .Validate(new CreateDeferredEntityResolutionPromisesRequest { Entities = [] })
                        .IsValid
                        .Should()
                        .BeFalse();

                    new ResolveExternalEntityReferencesRequestValidator()
                        .Validate(new ResolveExternalEntityReferencesRequest { DataHubEntitiesToResolve = [] })
                        .IsValid
                        .Should()
                        .BeFalse();

                    new ResolveEntityReferenceResolutionPromisesRequestValidator()
                        .Validate(new ResolveEntityReferenceResolutionPromisesRequest { SourceSystemEntityIds = [], ResolvedReferencedEntities = [] })
                        .IsValid
                        .Should()
                        .BeFalse();

                    return await ActionResult(currentObject, stash);
                });
            })
            .Then("The assertions complete successfully", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .Run();
    }

    private static JObject Entity(string id, string entityType, JObject? extra = null, DateTimeOffset? lastUpdated = null)
    {
        var entity = new JObject
        {
            [nameof(DataHubEntity.id)] = id,
            [nameof(DataHubEntity.entityType)] = entityType,
            [nameof(DataHubEntity.createdOn)] = DateTimeOffset.Parse("2024-01-01T00:00:00Z"),
            [nameof(DataHubEntity.lastUpdated)] = lastUpdated ?? DateTimeOffset.Parse("2024-01-01T00:00:00Z"),
            [nameof(DataHubEntity.alternateKeys)] = new JArray()
        };

        if (extra != null)
        {
            foreach (var property in extra.Properties())
            {
                entity[property.Name] = property.Value;
            }
        }

        return entity;
    }

    private static JObject Reference(string dataSource, string sourceEntityType, string entityType, string entityId, DateTimeOffset? timestamp = null)
    {
        return JObject.FromObject(new ExternalEntityReference
        {
            DataSource = dataSource,
            SourceEntityType = sourceEntityType,
            EntityType = entityType,
            EntityId = entityId,
            Timestamp = timestamp
        });
    }

    private static string LookupValue(FindMatchingEntitiesQuery query)
    {
        return query.Parameters.Single(parameter => parameter.Name == "entityId0").Value?.ToString()!;
    }

    private static AddTrackedEntityChangeSetsResponse TrackingSuccess()
    {
        return new AddTrackedEntityChangeSetsResponse { Successes = [], Failures = [] };
    }

    private static UpsertDataHubEntitiesResponse UpsertSuccess(UpsertDataHubEntitiesCommand command)
    {
        return new UpsertDataHubEntitiesResponse { Successes = command.Entities, Failures = [] };
    }

    private static JArray FoundEntity(string sourceEntityId, string entityType, string? dataHubEntityId = null)
    {
        return new JArray(Entity(dataHubEntityId ?? $"dh-{sourceEntityId}", entityType, new JObject
        {
            [nameof(DataHubEntity.alternateKeys)] = JArray.FromObject(new List<AlternateKey>
            {
                new("src1.typea", sourceEntityId),
                new("src1.typeb", sourceEntityId)
            })
        }));
    }

    private static ResolutionPromise Promise(
        string id,
        string ownerType,
        string ownerId,
        string path,
        string dataSource,
        string sourceEntityType,
        string entityType,
        string entityId,
        DateTimeOffset? timestamp = null)
    {
        return new ResolutionPromise
        {
            id = id,
            DataHubEntityType = ownerType,
            DataHubEntityId = ownerId,
            EntityReferencePath = path,
            ExternalEntityReference = new ExternalEntityReference
            {
                DataSource = dataSource,
                SourceEntityType = sourceEntityType,
                EntityType = entityType,
                EntityId = entityId,
                Timestamp = timestamp
            }
        };
    }

    private static ResolvedEntityReference Resolved(string dataSource, string sourceEntityType, string sourceEntityId, string entityType, string entityId)
    {
        return new ResolvedEntityReference
        {
            SourceEntityReference = new ExternalEntityReference
            {
                DataSource = dataSource,
                SourceEntityType = sourceEntityType,
                EntityType = entityType,
                EntityId = sourceEntityId
            },
            DataHubEntityReference = new EntityReference
            {
                EntityType = entityType,
                EntityId = entityId
            }
        };
    }

    private static void AssertResolvedReference(JObject entity, string path, string entityType, string entityId)
    {
        var reference = entity.SelectToken(path).Should().BeOfType<JObject>().Subject;
        reference.Value<string>("@Tag").Should().BeNullOrEmpty();
        reference.Value<string>(nameof(EntityReference.EntityType)).Should().Be(entityType);
        reference.Value<string>(nameof(EntityReference.EntityId)).Should().Be(entityId);
    }

    private static void AssertExternalReference(JObject entity, string path, string dataSource, string sourceEntityType, string entityType, string entityId)
    {
        var reference = entity.SelectToken(path).Should().BeOfType<JObject>().Subject;
        reference.Value<string>("@Tag").Should().Be(nameof(ExternalEntityReference));
        reference.Value<string>(nameof(ExternalEntityReference.DataSource)).Should().Be(dataSource);
        reference.Value<string>(nameof(ExternalEntityReference.SourceEntityType)).Should().Be(sourceEntityType);
        reference.Value<string>(nameof(EntityReference.EntityType)).Should().Be(entityType);
        reference.Value<string>(nameof(EntityReference.EntityId)).Should().Be(entityId);
    }

    private static DeleteCosmosDocumentsResponse<ResolutionPromise> CaptureDelete(DeleteCosmosDocumentsCommand<ResolutionPromise> command, ref DeleteCosmosDocumentsCommand<ResolutionPromise>? capturedCommand)
    {
        capturedCommand = command;
        return new DeleteCosmosDocumentsResponse<ResolutionPromise> { Successes = command.Documents, Failures = [] };
    }

    private static TResponse Capture<TRequest, TResponse>(TRequest request, ref TRequest? capturedRequest, TResponse response)
        where TRequest : class
    {
        capturedRequest = request;
        return response;
    }

    private sealed class FixedTimeService(DateTimeOffset now) : ITimeService
    {
        public DateTimeOffset Now()
        {
            return now;
        }
    }

    private sealed class RecordingMediator(Func<IRequest, object> responseFactory) : IMediator
    {
        public List<IRequest> Requests { get; } = [];

        public Task<OneOf<TResponse, Exception>> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult<OneOf<TResponse, Exception>>((TResponse)responseFactory(request));
        }

        public Task<OneOf<object, Exception>> SendAsync(IRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult<OneOf<object, Exception>>(responseFactory(request));
        }

        public Task<object> SendAndHandleExceptions<TRequest>(TRequest request, CancellationToken cancellationToken, Action<Exception>? exceptionHandler = null)
            where TRequest : IRequest
        {
            throw new NotSupportedException();
        }

        public Task<TResponse> SendAndHandleExceptions<TResponse>(IRequest request, CancellationToken cancellationToken, Action<Exception>? exceptionHandler = null)
        {
            throw new NotSupportedException();
        }

        public Task<(TResponse? Response, Exception? Exception)> TrySend<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken, Action<Exception>? exceptionHandler = null)
        {
            Requests.Add(request);
            return Task.FromResult(((TResponse?)responseFactory(request), (Exception?)null));
        }
    }
}
