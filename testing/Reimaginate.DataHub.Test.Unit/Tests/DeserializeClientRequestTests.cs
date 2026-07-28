using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.Requests.External.Client.DeserializeClientRequest;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Exceptions;
using Reimaginate.DataHub.SharedModels.Requests.Client;
using Xunit;
using Reimaginate.DataHub.Test.Unit.Base;
using Reimaginate.Test.Framework;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class DeserializeClientRequestTests : ScenarioUnitTestBase
{
    [Fact]
    public async Task HandleAsync_should_throw_diagnostic_exception_for_missing_envelope()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(HandleAsync_should_throw_diagnostic_exception_for_missing_envelope)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(HandleAsync_should_throw_diagnostic_exception_for_missing_envelope))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var handler = new DeserializeClientRequestRequestHandler();

                    var act = () => handler.HandleAsync(new DeserializeClientRequestRequest(), CancellationToken.None);

                    var exception = await act.Should().ThrowAsync<DataHubInvalidRequestException>();
                    exception.Which.Message.Should().Be("Request envelope is required.");
                    exception.Which.Category.Should().Be(DataHubErrorCategory.InvalidRequest);

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
    public async Task HandleAsync_should_throw_diagnostic_exception_for_missing_request_type()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(HandleAsync_should_throw_diagnostic_exception_for_missing_request_type)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(HandleAsync_should_throw_diagnostic_exception_for_missing_request_type))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var handler = new DeserializeClientRequestRequestHandler();

                    var act = () => handler.HandleAsync(new DeserializeClientRequestRequest
                    {
                        SerializedRequest = new SerializedRequest
                        {
                            CorrelationId = "corr-missing-type",
                            Data = "{}"
                        }
                    }, CancellationToken.None);

                    var exception = await act.Should().ThrowAsync<DataHubInvalidRequestException>();
                    exception.Which.Message.Should().Be("RequestType is required.");
                    exception.Which.CorrelationId.Should().Be("corr-missing-type");
                    exception.Which.Category.Should().Be(DataHubErrorCategory.InvalidRequest);

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
    public async Task HandleAsync_should_deserialize_known_client_request_and_parse_datetime_offsets()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(HandleAsync_should_deserialize_known_client_request_and_parse_datetime_offsets)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(HandleAsync_should_deserialize_known_client_request_and_parse_datetime_offsets))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var handler = new DeserializeClientRequestRequestHandler();
                    var timestamp = DateTimeOffset.Parse("2024-01-02T03:04:05+10:00");

                    var result = await handler.HandleAsync(new DeserializeClientRequestRequest
                    {
                        SerializedRequest = new SerializedRequest
                        {
                            RequestType = nameof(UpdateEntityRequest),
                            CorrelationId = "corr-123",
                            Data = JsonConvert.SerializeObject(new
                            {
                                DataSource = "SRC1",
                                EntityType = "DHType",
                                EntityId = "entity-1",
                                Timestamp = timestamp,
                                UnknownField = "ignored",
                                Data = new JObject
                                {
                                    ["Name"] = "Updated"
                                }
                            })
                        }
                    }, CancellationToken.None);

                    var request = result.Should().BeOfType<UpdateEntityRequest>().Subject;
                    request.DataSource.Should().Be("SRC1");
                    request.EntityType.Should().Be("DHType");
                    request.EntityId.Should().Be("entity-1");
                    request.CorrelationId.Should().Be("corr-123");
                    request.Timestamp.Should().Be(timestamp);
                    request.Data.Value<string>("Name").Should().Be("Updated");

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
    public async Task HandleAsync_should_apply_trace_options_from_client_envelope()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(HandleAsync_should_apply_trace_options_from_client_envelope)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(HandleAsync_should_apply_trace_options_from_client_envelope))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var handler = new DeserializeClientRequestRequestHandler();
                    var expiresOn = DateTimeOffset.UtcNow.AddMinutes(30);
                    var traceOptions = new DataHubTraceOptions
                    {
                        Enabled = true,
                        IncludeRequest = true,
                        IncludeResponse = true,
                        Reason = "investigate merge failure",
                        ExpiresOn = expiresOn
                    };

                    var result = await handler.HandleAsync(new DeserializeClientRequestRequest
                    {
                        SerializedRequest = new SerializedRequest
                        {
                            RequestType = nameof(UpdateEntityRequest),
                            CorrelationId = "corr-trace-client",
                            TraceOptions = traceOptions,
                            Data = JsonConvert.SerializeObject(new
                            {
                                DataSource = "SRC1",
                                EntityType = "DHType",
                                EntityId = "entity-1",
                                Timestamp = DateTimeOffset.UtcNow,
                                Data = new JObject
                                {
                                    ["Name"] = "Updated"
                                }
                            })
                        }
                    }, CancellationToken.None);

                    var request = result.Should().BeOfType<UpdateEntityRequest>().Subject;
                    request.CorrelationId.Should().Be("corr-trace-client");
                    request.TraceOptions.Should().BeEquivalentTo(traceOptions);

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
    public async Task HandleAsync_should_deserialize_entity_batch_request_envelopes()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(HandleAsync_should_deserialize_entity_batch_request_envelopes)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(HandleAsync_should_deserialize_entity_batch_request_envelopes))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var handler = new DeserializeClientRequestRequestHandler();
                    var timestamp = DateTimeOffset.Parse("2024-02-03T04:05:06+10:00");

                    var mergeEntities = await DeserializeAsync<MergeEntitiesRequest>(handler, nameof(MergeEntitiesRequest), new
                    {
                        DataSource = "SRC1",
                        AgentId = "agent-1",
                        Requests = new[]
                        {
                            new
                            {
                                DataSource = "SRC1",
                                SourceEntityType = "TypeA",
                                SourceEntityId = "source-1",
                                DataHubEntityType = "DHType",
                                Data = new JObject { ["ValueA"] = "Merged" }
                            }
                        }
                    });

                    mergeEntities.CorrelationId.Should().Be("corr-MergeEntitiesRequest");
                    mergeEntities.DataSource.Should().Be("SRC1");
                    mergeEntities.AgentId.Should().Be("agent-1");
                    mergeEntities.Requests.Should().ContainSingle();
                    mergeEntities.Requests[0].Data.Value<string>("ValueA").Should().Be("Merged");

                    var mergeUntracked = await DeserializeAsync<MergeUntrackedEntitiesRequest>(handler, nameof(MergeUntrackedEntitiesRequest), new
                    {
                        DataSource = "SRC2",
                        Requests = new[]
                        {
                            new
                            {
                                DataSource = "SRC2",
                                SourceEntityType = "TypeB",
                                SourceEntityId = "source-2",
                                DataHubEntityType = "DHTypeB",
                                Data = new JObject { ["ValueA"] = "Merged Untracked" }
                            }
                        }
                    });

                    mergeUntracked.CorrelationId.Should().Be("corr-MergeUntrackedEntitiesRequest");
                    mergeUntracked.DataSource.Should().Be("SRC2");
                    mergeUntracked.Requests.Should().ContainSingle();
                    mergeUntracked.Requests[0].DataHubEntityType.Should().Be("DHTypeB");

                    var updateEntities = await DeserializeAsync<UpdateEntitiesRequest>(handler, nameof(UpdateEntitiesRequest), new
                    {
                        Requests = new[]
                        {
                            new
                            {
                                DataSource = "SRC3",
                                EntityType = "TypeC",
                                EntityId = "entity-3",
                                Timestamp = timestamp,
                                UpdateType = "patch",
                                CreateIfMissing = true,
                                ReturnResultingEntity = true,
                                Data = new JObject { ["ValueA"] = "Updated" }
                            }
                        }
                    });

                    updateEntities.CorrelationId.Should().Be("corr-UpdateEntitiesRequest");
                    updateEntities.Requests.Should().ContainSingle();
                    updateEntities.Requests[0].Timestamp.Should().Be(timestamp);
                    updateEntities.Requests[0].UpdateType.Should().Be("patch");
                    updateEntities.Requests[0].CreateIfMissing.Should().BeTrue();
                    updateEntities.Requests[0].ReturnResultingEntity.Should().BeTrue();

                    var updateUntracked = await DeserializeAsync<UpdateUntrackedEntitiesRequest>(handler, nameof(UpdateUntrackedEntitiesRequest), new
                    {
                        Silent = true,
                        DispatchNotifications = false,
                        Requests = new[]
                        {
                            new
                            {
                                EntityType = "DHType",
                                EntityId = "entity-4",
                                CreateIfMissing = false,
                                Silent = false,
                                DispatchNotifications = true,
                                Data = new JObject { ["ValueA"] = "Updated Untracked" }
                            }
                        }
                    });

                    updateUntracked.CorrelationId.Should().Be("corr-UpdateUntrackedEntitiesRequest");
                    updateUntracked.Silent.Should().BeTrue();
                    updateUntracked.DispatchNotifications.Should().BeFalse();
                    updateUntracked.Requests.Should().ContainSingle();
                    updateUntracked.Requests[0].CreateIfMissing.Should().BeFalse();
                    updateUntracked.Requests[0].DispatchNotifications.Should().BeTrue();
                    updateUntracked.Requests[0].Data.Value<string>("ValueA").Should().Be("Updated Untracked");

                    var reserveAutoNumbers = await DeserializeAsync<ReserveAutoNumbersRequest>(handler, nameof(ReserveAutoNumbersRequest), new
                    {
                        SequenceName = "orders",
                        Count = 10
                    });

                    reserveAutoNumbers.CorrelationId.Should().Be("corr-ReserveAutoNumbersRequest");
                    reserveAutoNumbers.SequenceName.Should().Be("orders");
                    reserveAutoNumbers.Count.Should().Be(10);

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
    public async Task HandleAsync_should_deserialize_client_query_parameters()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(HandleAsync_should_deserialize_client_query_parameters)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(HandleAsync_should_deserialize_client_query_parameters))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var handler = new DeserializeClientRequestRequestHandler();

                    var result = await handler.HandleAsync(new DeserializeClientRequestRequest
                    {
                        SerializedRequest = new SerializedRequest
                        {
                            RequestType = nameof(GetDataHubEntitiesWhereRequest),
                            CorrelationId = "corr-query-params",
                            Data = JsonConvert.SerializeObject(new
                            {
                                WhereClause = "x.id = @id and x.Count = @count and x.Enabled = @enabled and x.Optional = @optional",
                                Parameters = new object[]
                                {
                                    new { Name = "id", Value = "entity-1" },
                                    new { Name = "count", Value = 42 },
                                    new { Name = "enabled", Value = true },
                                    new { Name = "optional", Value = (object?)null }
                                }
                            })
                        }
                    }, CancellationToken.None);

                    var request = result.Should().BeOfType<GetDataHubEntitiesWhereRequest>().Subject;
                    request.Parameters.Should().HaveCount(4);
                    request.Parameters.Should().Contain(parameter => parameter.Name == "id" && Equals(parameter.Value, "entity-1"));
                    request.Parameters.Should().Contain(parameter => parameter.Name == "count" && Convert.ToInt32(parameter.Value) == 42);
                    request.Parameters.Should().Contain(parameter => parameter.Name == "enabled" && Equals(parameter.Value, true));
                    request.Parameters.Should().Contain(parameter => parameter.Name == "optional" && parameter.Value == null);

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
    public async Task HandleAsync_should_throw_for_invalid_request_type()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(HandleAsync_should_throw_for_invalid_request_type)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(HandleAsync_should_throw_for_invalid_request_type))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var handler = new DeserializeClientRequestRequestHandler();

                    var act = () => handler.HandleAsync(new DeserializeClientRequestRequest
                    {
                        SerializedRequest = new SerializedRequest
                        {
                            RequestType = "NotARealRequest",
                            Data = "{}"
                        }
                    }, CancellationToken.None);

                    var exception = await act.Should().ThrowAsync<DataHubInvalidRequestException>();
                    exception.Which.Message.Should().Be("Invalid request type 'NotARealRequest'.");
                    exception.Which.RequestType.Should().Be("NotARealRequest");
                    exception.Which.Category.Should().Be(DataHubErrorCategory.InvalidRequest);

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
    public async Task HandleAsync_should_throw_diagnostic_exception_for_missing_client_data()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(HandleAsync_should_throw_diagnostic_exception_for_missing_client_data)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(HandleAsync_should_throw_diagnostic_exception_for_missing_client_data))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var handler = new DeserializeClientRequestRequestHandler();

                    var act = () => handler.HandleAsync(new DeserializeClientRequestRequest
                    {
                        SerializedRequest = new SerializedRequest
                        {
                            RequestType = nameof(UpdateEntityRequest),
                            CorrelationId = "corr-456"
                        }
                    }, CancellationToken.None);

                    var exception = await act.Should().ThrowAsync<DataHubInvalidRequestException>();
                    exception.Which.Message.Should().Be("Request Data is required.");
                    exception.Which.RequestType.Should().Be(nameof(UpdateEntityRequest));
                    exception.Which.CorrelationId.Should().Be("corr-456");

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
    public async Task HandleAsync_should_throw_diagnostic_exception_for_malformed_client_data()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(HandleAsync_should_throw_diagnostic_exception_for_malformed_client_data)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(HandleAsync_should_throw_diagnostic_exception_for_malformed_client_data))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var handler = new DeserializeClientRequestRequestHandler();

                    var act = () => handler.HandleAsync(new DeserializeClientRequestRequest
                    {
                        SerializedRequest = new SerializedRequest
                        {
                            RequestType = nameof(UpdateEntityRequest),
                            CorrelationId = "corr-789",
                            Data = JsonConvert.SerializeObject(new
                            {
                                Timestamp = "not-a-date"
                            })
                        }
                    }, CancellationToken.None);

                    var exception = await act.Should().ThrowAsync<DataHubDeserializationException>();
                    exception.Which.Category.Should().Be(DataHubErrorCategory.DeserializationFailed);
                    exception.Which.RequestType.Should().Be(nameof(UpdateEntityRequest));
                    exception.Which.CorrelationId.Should().Be("corr-789");
                    exception.Which.Details.Should().ContainSingle(detail => detail.Field == nameof(UpdateEntityRequest.Timestamp));

                    return await ActionResult(currentObject, stash);
                });
            })
            .Then("The assertions complete successfully", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .Run();
    }

    private static async Task<T> DeserializeAsync<T>(DeserializeClientRequestRequestHandler handler, string requestType, object data)
    {
        var result = await handler.HandleAsync(new DeserializeClientRequestRequest
        {
            SerializedRequest = new SerializedRequest
            {
                RequestType = requestType,
                CorrelationId = $"corr-{requestType}",
                Data = JsonConvert.SerializeObject(data)
            }
        }, CancellationToken.None);

        return result.Should().BeOfType<T>().Subject;
    }
}
