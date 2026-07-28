using FluentValidation;

namespace Reimaginate.DataHub.Requests.Internal.CreateChangeTrackingEntry;

public class CreateChangeTrackingEntryRequestValidator : AbstractValidator<CreateChangeTrackingEntryRequest>
{
    public CreateChangeTrackingEntryRequestValidator()
    {
        RuleFor(r => r.EntryType).NotNull();
        RuleFor(r => r.DataSource).NotNull();
        RuleFor(r => r.EntityType).NotNull();
        RuleFor(r => r.EntityId).NotNull();
        RuleFor(r => r.Data).NotNull();
    }
}