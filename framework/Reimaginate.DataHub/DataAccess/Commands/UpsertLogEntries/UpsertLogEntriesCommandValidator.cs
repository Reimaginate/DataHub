using FluentValidation;

// ReSharper disable IdentifierTypo

namespace Reimaginate.DataHub.DataAccess.Commands.UpsertLogEntries;

public class UpsertLogEntriesCommandValidator : AbstractValidator<UpsertLogEntriesCommand>
{
    public UpsertLogEntriesCommandValidator()
    {
        RuleFor(x => x.LogEntries).NotEmpty();
    }
}