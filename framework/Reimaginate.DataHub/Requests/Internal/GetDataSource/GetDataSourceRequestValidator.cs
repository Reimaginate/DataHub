using FluentValidation;

namespace Reimaginate.DataHub.Requests.Internal.GetDataSource;

public class GetDataSourceRequestValidator : AbstractValidator<GetDataSourceRequest>
{
    public GetDataSourceRequestValidator()
    {
        RuleFor(r => r.DataSourceName).NotEmpty();
    }
}