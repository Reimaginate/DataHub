using FluentAssertions;
using Reimaginate.DataHub.Services.AutoNumbers;
using Reimaginate.DataHub.SharedModels.Core;
using Xunit;
using Reimaginate.DataHub.Test.Unit.Base;
using Reimaginate.Test.Framework;

namespace Reimaginate.DataHub.Test.Unit.Tests;

public class AutoNumberServiceTests : ScenarioUnitTestBase
{
    [Fact]
    public async Task ReserveAsync_should_format_prefix_padding_and_suffix()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(ReserveAsync_should_format_prefix_padding_and_suffix)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(ReserveAsync_should_format_prefix_padding_and_suffix))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var sequence = new AutoNumberSequence
                    {
                        id = "accounts",
                        SequenceName = "accounts",
                        Prefix = "ACC-",
                        Suffix = "-A",
                        PaddingLength = 6,
                        StartAt = 1,
                        IncrementBy = 1,
                        CurrentValue = 0,
                        _etag = "etag-1"
                    };

                    var service = new AutoNumberService(new InMemoryAutoNumberSequenceStore(sequence));

                    var numbers = await service.ReserveAsync("accounts", 1, CancellationToken.None);

                    numbers.Should().ContainSingle().Which.Should().BeEquivalentTo(new
                    {
                        SequenceValue = 1L,
                        Value = "ACC-000001-A"
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
    public async Task ReserveAsync_should_honor_start_increment_and_batch_order()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(ReserveAsync_should_honor_start_increment_and_batch_order)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(ReserveAsync_should_honor_start_increment_and_batch_order))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var sequence = new AutoNumberSequence
                    {
                        id = "orders",
                        SequenceName = "orders",
                        Prefix = "ORD-",
                        PaddingLength = 3,
                        StartAt = 10,
                        IncrementBy = 5,
                        CurrentValue = 0,
                        _etag = "etag-1"
                    };

                    var service = new AutoNumberService(new InMemoryAutoNumberSequenceStore(sequence));

                    var firstBatch = await service.ReserveAsync("orders", 3, CancellationToken.None);
                    var secondBatch = await service.ReserveAsync("orders", 2, CancellationToken.None);

                    firstBatch.Select(n => n.SequenceValue).Should().Equal(10, 15, 20);
                    firstBatch.Select(n => n.Value).Should().Equal("ORD-010", "ORD-015", "ORD-020");
                    secondBatch.Select(n => n.SequenceValue).Should().Equal(25, 30);
                    secondBatch.Select(n => n.Value).Should().Equal("ORD-025", "ORD-030");
                    sequence.CurrentValue.Should().Be(30);

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
    public async Task ReserveAsync_should_retry_when_the_store_reports_a_concurrency_conflict()
    {
        await ScenarioBuilder.CreateScenario(TestDisplayName() ?? Humanize(nameof(ReserveAsync_should_retry_when_the_store_reports_a_concurrency_conflict)), base.ServiceProvider)
            .Given("The unit test context is isolated", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .When($"The behavior is exercised: {Humanize(nameof(ReserveAsync_should_retry_when_the_store_reports_a_concurrency_conflict))}", step =>
            {
                step.Exec(async (currentObject, stash) =>
                {
                    var sequence = new AutoNumberSequence
                    {
                        id = "cases",
                        SequenceName = "cases",
                        Prefix = "CAS-",
                        PaddingLength = 2,
                        StartAt = 1,
                        IncrementBy = 1,
                        CurrentValue = 0,
                        _etag = "etag-1"
                    };

                    var store = new InMemoryAutoNumberSequenceStore(sequence)
                    {
                        FailNextReplace = true
                    };
                    var service = new AutoNumberService(store);

                    var numbers = await service.ReserveAsync("cases", 2, CancellationToken.None);

                    numbers.Select(n => n.Value).Should().Equal("CAS-01", "CAS-02");
                    store.ReplaceAttempts.Should().Be(2);

                    return await ActionResult(currentObject, stash);
                });
            })
            .Then("The assertions complete successfully", step =>
            {
                step.Exec((currentObject, stash) => ActionResult(currentObject, stash));
            })
            .Run();
    }

    private sealed class InMemoryAutoNumberSequenceStore : IAutoNumberSequenceStore
    {
        private readonly AutoNumberSequence _sequence;
        private int _etagNumber = 1;

        public InMemoryAutoNumberSequenceStore(AutoNumberSequence sequence)
        {
            _sequence = sequence;
        }

        public bool FailNextReplace { get; set; }
        public int ReplaceAttempts { get; private set; }

        public Task<AutoNumberSequence> GetSequenceAsync(string sequenceName, CancellationToken cancellationToken)
        {
            if (_sequence.SequenceName != sequenceName)
            {
                throw new AutoNumberSequenceNotFoundException(sequenceName);
            }

            return Task.FromResult(Clone(_sequence));
        }

        public Task<AutoNumberSequence> TryIncrementSequenceAsync(AutoNumberSequence sequence, long incrementAmount, string etag, CancellationToken cancellationToken)
        {
            ReplaceAttempts++;

            if (FailNextReplace)
            {
                FailNextReplace = false;
                return Task.FromResult<AutoNumberSequence>(null!);
            }

            _sequence.CurrentValue += incrementAmount;
            _sequence._etag = $"etag-{++_etagNumber}";
            return Task.FromResult(Clone(_sequence));
        }

        private static AutoNumberSequence Clone(AutoNumberSequence sequence)
        {
            return new AutoNumberSequence
            {
                id = sequence.id,
                pk = sequence.pk,
                SequenceName = sequence.SequenceName,
                Prefix = sequence.Prefix,
                Suffix = sequence.Suffix,
                PaddingLength = sequence.PaddingLength,
                StartAt = sequence.StartAt,
                IncrementBy = sequence.IncrementBy,
                CurrentValue = sequence.CurrentValue,
                _etag = sequence._etag
            };
        }
    }
}
