using FluentValidation;
using Reimaginate.DataHub.SharedModels.Requests.Client;

namespace Reimaginate.DataHub.Requests.External.Client.DeleteJobs;

public class DeleteJobsRequestValidator : AbstractValidator<DeleteJobsRequest>
{
    public DeleteJobsRequestValidator()
    {
       
    }
}