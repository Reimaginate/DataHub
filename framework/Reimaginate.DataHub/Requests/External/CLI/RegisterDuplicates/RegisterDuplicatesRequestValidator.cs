using FluentValidation;
using Reimaginate.DataHub.SharedModels.Requests.CLI;

namespace Reimaginate.DataHub.Requests.External.CLI.RegisterDuplicates;

public class RegisterDuplicatesRequestValidator : AbstractValidator<RegisterDuplicatesRequest>
{
    public RegisterDuplicatesRequestValidator()
    {
       
    }
}