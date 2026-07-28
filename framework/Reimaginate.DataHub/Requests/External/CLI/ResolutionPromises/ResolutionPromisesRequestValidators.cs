using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.ResolutionPromises;

public class ListResolutionPromisesRequestValidator : AbstractValidator<ListResolutionPromisesRequest>
{
    public ListResolutionPromisesRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.QueryEntities)).Equal(true).WithMessage("Not Authorized");
        RuleFor(r => r.PageSize).GreaterThan(0);
    }
}

public class GetResolutionPromisesRequestValidator : AbstractValidator<GetResolutionPromisesRequest>
{
    public GetResolutionPromisesRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.QueryEntities)).Equal(true).WithMessage("Not Authorized");
        RuleFor(r => r.PromiseIds).NotEmpty();
    }
}

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

public class PatchResolutionPromiseRequestValidator : AbstractValidator<PatchResolutionPromiseRequest>
{
    public PatchResolutionPromiseRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.PatchEntities)).Equal(true).WithMessage("Not Authorized");
        RuleFor(r => r.PromiseId).NotEmpty();
        RuleFor(r => r.Operations).NotEmpty();
    }
}
