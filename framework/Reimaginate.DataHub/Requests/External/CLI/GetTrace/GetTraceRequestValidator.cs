using FluentValidation;
using Reimaginate.DataHub.Auth;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.GetTrace;

public class GetTraceRequestValidator : AbstractValidator<GetTraceRequest>
{
    public GetTraceRequestValidator()
    {
        RuleFor(r => Authorization.HasPermissions(r.User, DataHubPermissions.QueryDiagnostics)).Equal(true).WithMessage("Not Authorized");
        RuleFor(r => r.TraceCorrelationId).NotEmpty();
    }
}
