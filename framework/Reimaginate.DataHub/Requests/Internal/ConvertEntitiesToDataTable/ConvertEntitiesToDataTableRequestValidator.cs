using FluentValidation;

namespace Reimaginate.DataHub.Requests.Internal.ConvertEntitiesToDataTable;

public class ConvertEntitiesToDataTableRequestValidator : AbstractValidator<ConvertEntitiesToDataTableRequest>
{
    public ConvertEntitiesToDataTableRequestValidator()
    {
        RuleFor(r => r.TableName).NotEmpty();
        RuleFor(r => r.Entities).NotNull();
        RuleFor(r => r.Columns).NotEmpty();
    }
}