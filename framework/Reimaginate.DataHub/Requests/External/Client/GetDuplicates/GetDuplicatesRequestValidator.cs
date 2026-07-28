using FluentValidation;
using Reimaginate.DataHub.SharedModels.Requests.Client;

namespace Reimaginate.DataHub.Requests.External.Client.GetDuplicates;

public class GetDuplicatesRequestValidator : AbstractValidator<GetDuplicatesRequest>
{
    public GetDuplicatesRequestValidator()
    {
       
    }
}