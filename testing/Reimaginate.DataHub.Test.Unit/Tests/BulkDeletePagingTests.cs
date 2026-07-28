using FluentAssertions;
using Reimaginate.DataHub.DataAccess.Commands.DeleteCosmosDocuments;
using Reimaginate.DataHub.DataAccess.Queries.GetCosmosDocuments;
using Reimaginate.DataHub.Models;
using Reimaginate.DataHub.Requests.Internal.DeleteLogEntries;
using Reimaginate.DataHub.Requests.Internal.ProcessDeleteJobs;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Core.Models.Jobs;
using Reimaginate.DataServices.Responses;
using Reimaginate.Mediator;
using Xunit;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class BulkDeletePagingTests
{
    [Fact]
    public async Task ProcessDeleteJobs_should_requery_first_page_after_successful_paged_delete()
    {
        var firstJob = new Job { id = "job-1" };
        var secondJob = new Job { id = "job-2" };
        var jobPages = new Queue<PagedResults<Job>>(
        [
            new() { Results = [firstJob], MoreResultsAvailable = true, ContinuationToken = "stale-page-token" },
            new() { Results = [secondJob], MoreResultsAvailable = false }
        ]);

        var mediator = new RecordingMediator(request => request switch
        {
            GetCosmosDocumentsQuery<Job> => jobPages.Dequeue(),
            DeleteCosmosDocumentsCommand<Job> command => new DeleteCosmosDocumentsResponse<Job>
            {
                Successes = command.Documents.ToList(),
                Failures = []
            },
            _ => throw new NotSupportedException(request.GetType().FullName)
        });

        var response = await new ProcessDeleteJobsRequestHandler(mediator).HandleAsync(new ProcessDeleteJobsRequest
        {
            JobIds = ["job-1", "job-2"]
        }, CancellationToken.None);

        response.Success.Should().BeTrue();
        var queries = mediator.Requests.OfType<GetCosmosDocumentsQuery<Job>>().ToList();
        queries.Should().HaveCount(2);
        queries[0].ContinuationToken.Should().BeNullOrEmpty();
        queries[1].ContinuationToken.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task DeleteLogEntries_should_stop_without_continuation_after_failed_delete_page()
    {
        var logEntry = new LogEntry { id = "log-1", Type = "PatchFailure" };
        var mediator = new RecordingMediator(request => request switch
        {
            GetCosmosDocumentsQuery<LogEntry> => new PagedResults<LogEntry>
            {
                Results = [logEntry],
                MoreResultsAvailable = true,
                ContinuationToken = "stale-page-token"
            },
            DeleteCosmosDocumentsCommand<LogEntry> command => new DeleteCosmosDocumentsResponse<LogEntry>
            {
                Successes = [],
                Failures = [new DataAccessFailure<LogEntry>(command.Documents.Single(), new InvalidOperationException("delete failed"))]
            },
            _ => throw new NotSupportedException(request.GetType().FullName)
        });

        var response = await new DeleteLogEntriesRequestHandler(mediator).HandleAsync(new DeleteLogEntriesRequest
        {
            Ids = ["log-1"]
        }, CancellationToken.None);

        response.Success.Should().BeFalse();
        mediator.Requests.OfType<GetCosmosDocumentsQuery<LogEntry>>().Should().HaveCount(1);
        mediator.Requests.OfType<GetCosmosDocumentsQuery<LogEntry>>().Single().ContinuationToken.Should().BeNullOrEmpty();
    }

    private sealed class RecordingMediator(Func<IRequest, object> responseFactory) : IMediator
    {
        public List<IRequest> Requests { get; } = [];

        public Task<OneOf.OneOf<TResponse, Exception>> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<OneOf.OneOf<object, Exception>> SendAsync(IRequest request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<object> SendAndHandleExceptions<TRequest>(
            TRequest request,
            CancellationToken cancellationToken,
            Action<Exception>? exceptionHandler = null)
            where TRequest : IRequest
        {
            throw new NotSupportedException();
        }

        public Task<TResponse> SendAndHandleExceptions<TResponse>(
            IRequest request,
            CancellationToken cancellationToken,
            Action<Exception>? exceptionHandler = null)
        {
            throw new NotSupportedException();
        }

        public Task<(TResponse? Response, Exception? Exception)> TrySend<TResponse>(
            IRequest<TResponse> request,
            CancellationToken cancellationToken,
            Action<Exception>? exceptionHandler = null)
        {
            Requests.Add(request);
            return Task.FromResult(((TResponse?)responseFactory(request), (Exception?)null));
        }
    }
}
