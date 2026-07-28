using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Reimaginate.DataHub.DataAccess.Commands.DeleteCosmosDocuments;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataServices;
using Reimaginate.DataServices.Responses;
using Xunit;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class DeleteCosmosDocumentsCommandHandlerTests
{
    [Fact]
    public async Task DeleteCosmosDocuments_should_count_submitted_documents_as_successful_when_bulk_delete_reports_no_failures()
    {
        var promise = Promise("promise-1");
        var dataService = Substitute.For<IPartitionedDataService<ResolutionPromise>>();
        dataService.BulkDeleteItemsAsync(Arg.Any<List<ResolutionPromise>>(), Arg.Any<CancellationToken>())
            .Returns(new BulkOperationResults<ResolutionPromise>
            {
                Successes = [],
                Failures = []
            });
        var handler = new DeleteCosmosDocumentsCommandHandler<ResolutionPromise>(ServiceProvider(dataService));

        var response = await handler.HandleAsync(new DeleteCosmosDocumentsCommand<ResolutionPromise>
        {
            Documents = [promise]
        }, CancellationToken.None);

        response.Successes.Should().ContainSingle().Which.id.Should().Be("promise-1");
        response.Failures.Should().BeEmpty();
        await dataService.DidNotReceive().DeleteItemsAsync(Arg.Any<List<ResolutionPromise>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteCosmosDocuments_should_run_fallback_delete_only_for_bulk_delete_failures()
    {
        var succeeded = Promise("promise-1");
        var failed = Promise("promise-2");
        List<ResolutionPromise>? fallbackDocuments = null;
        var dataService = Substitute.For<IPartitionedDataService<ResolutionPromise>>();
        dataService.BulkDeleteItemsAsync(Arg.Any<List<ResolutionPromise>>(), Arg.Any<CancellationToken>())
            .Returns(new BulkOperationResults<ResolutionPromise>
            {
                Successes = [succeeded],
                Failures = [new BulkOperationFailure<ResolutionPromise>(failed, new InvalidOperationException("Bulk delete failed."))]
            });
        dataService.DeleteItemsAsync(Arg.Any<List<ResolutionPromise>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                fallbackDocuments = call.ArgAt<List<ResolutionPromise>>(0);
                return new DeleteResult();
            });
        var handler = new DeleteCosmosDocumentsCommandHandler<ResolutionPromise>(ServiceProvider(dataService));

        var response = await handler.HandleAsync(new DeleteCosmosDocumentsCommand<ResolutionPromise>
        {
            Documents = [succeeded, failed]
        }, CancellationToken.None);

        fallbackDocuments.Should().ContainSingle().Which.id.Should().Be("promise-2");
        response.Successes.Select(promise => promise.id).Should().BeEquivalentTo(["promise-1", "promise-2"]);
        response.Failures.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteCosmosDocuments_should_preserve_bulk_failure_when_fallback_delete_fails()
    {
        var failed = Promise("promise-1");
        var dataService = Substitute.For<IPartitionedDataService<ResolutionPromise>>();
        dataService.BulkDeleteItemsAsync(Arg.Any<List<ResolutionPromise>>(), Arg.Any<CancellationToken>())
            .Returns(new BulkOperationResults<ResolutionPromise>
            {
                Successes = [],
                Failures = [new BulkOperationFailure<ResolutionPromise>(failed, new InvalidOperationException("Bulk delete failed."))]
            });
        dataService.DeleteItemsAsync(Arg.Any<List<ResolutionPromise>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<DeleteResult>(new InvalidOperationException("Fallback delete failed.")));
        var handler = new DeleteCosmosDocumentsCommandHandler<ResolutionPromise>(ServiceProvider(dataService));

        var response = await handler.HandleAsync(new DeleteCosmosDocumentsCommand<ResolutionPromise>
        {
            Documents = [failed]
        }, CancellationToken.None);

        response.Successes.Should().BeEmpty();
        response.Failures.Should().ContainSingle().Which.Item.id.Should().Be("promise-1");
    }

    [Fact]
    public async Task DeleteCosmosDocuments_should_ignore_null_and_idless_delete_results()
    {
        var promise = Promise("promise-1");
        var dataService = Substitute.For<IPartitionedDataService<ResolutionPromise>>();
        dataService.BulkDeleteItemsAsync(Arg.Any<List<ResolutionPromise>>(), Arg.Any<CancellationToken>())
            .Returns(new BulkOperationResults<ResolutionPromise>
            {
                Successes = [null!, new ResolutionPromise(), promise],
                Failures =
                [
                    null!,
                    new BulkOperationFailure<ResolutionPromise>(null!, new InvalidOperationException("Missing document.")),
                    new BulkOperationFailure<ResolutionPromise>(new ResolutionPromise(), new InvalidOperationException("Missing id."))
                ]
            });
        var handler = new DeleteCosmosDocumentsCommandHandler<ResolutionPromise>(ServiceProvider(dataService));

        var response = await handler.HandleAsync(new DeleteCosmosDocumentsCommand<ResolutionPromise>
        {
            Documents = [promise]
        }, CancellationToken.None);

        response.Successes.Should().ContainSingle().Which.id.Should().Be("promise-1");
        response.Failures.Should().BeEmpty();
        await dataService.DidNotReceive().DeleteItemsAsync(Arg.Any<List<ResolutionPromise>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteCosmosDocuments_should_ignore_null_and_idless_submitted_documents()
    {
        var promise = Promise("promise-1");
        var dataService = Substitute.For<IPartitionedDataService<ResolutionPromise>>();
        dataService.BulkDeleteItemsAsync(Arg.Any<List<ResolutionPromise>>(), Arg.Any<CancellationToken>())
            .Returns(new BulkOperationResults<ResolutionPromise>
            {
                Successes = [],
                Failures = []
            });
        var handler = new DeleteCosmosDocumentsCommandHandler<ResolutionPromise>(ServiceProvider(dataService));

        var response = await handler.HandleAsync(new DeleteCosmosDocumentsCommand<ResolutionPromise>
        {
            Documents = [null!, new ResolutionPromise(), promise]
        }, CancellationToken.None);

        response.Successes.Should().ContainSingle().Which.id.Should().Be("promise-1");
        response.Failures.Should().BeEmpty();
    }

    private static ServiceProvider ServiceProvider(IPartitionedDataService<ResolutionPromise> dataService)
    {
        return new ServiceCollection()
            .AddSingleton(dataService)
            .BuildServiceProvider();
    }

    private static ResolutionPromise Promise(string id)
    {
        return new ResolutionPromise
        {
            id = id,
            pk = string.Empty
        };
    }
}
