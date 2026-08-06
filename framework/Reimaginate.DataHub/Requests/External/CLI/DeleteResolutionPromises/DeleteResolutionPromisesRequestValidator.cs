using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.DeleteResolutionPromises;

public class DeleteResolutionPromisesRequestValidator : AbstractValidator<DeleteResolutionPromisesRequest>
{
    public DeleteResolutionPromisesRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.PatchEntities)).Equal(true).WithMessage("Not Authorized");
        RuleFor(r => r.PageSize).GreaterThan(0);
        RuleFor(r => r)
            .Must(HasExactlyOneTargetMode)
            .WithMessage("Specify exactly one target mode: PromiseIds or WhereClause.");
    }

    private static bool HasExactlyOneTargetMode(DeleteResolutionPromisesRequest request)
    {
        var hasIds = request.PromiseIds is { Count: > 0 };
        var hasWhere = !string.IsNullOrWhiteSpace(request.WhereClause);
        return hasIds != hasWhere;
    }
}
