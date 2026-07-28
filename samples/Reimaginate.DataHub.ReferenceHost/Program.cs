using Reimaginate.DataHub.AspNetCore;
using Reimaginate.DataHub.AspNetCore.Observability;
using Reimaginate.DataHub.AspNetCore.Readiness;
using Reimaginate.DataHub.Config;
using Reimaginate.DataHub.Requests.Internal.GetUser;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Information);
});

builder.Services.AddDataHub(options => options.WithAppSettingsConfig(builder.Configuration, "DataHub"));
builder.Services.AddDataHubAzureMonitorObservability(builder.Configuration);

var clientAuthorizationMode = builder.Configuration["ReferenceHost:Client:AuthorizationMode"] ?? "SharedKey";
if (string.Equals(clientAuthorizationMode, "AzureAdBearer", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddDataHubClientAzureAdAuthorization(options =>
    {
        var section = builder.Configuration.GetSection("ReferenceHost:Client:AzureAd");
        options.TenantId = section["TenantId"];
        options.Audience = section["Audience"];
        options.AllowedClientIds = section.GetSection("AllowedClientIds").Get<string[]>() ?? Array.Empty<string>();
        options.AllowedObjectIds = section.GetSection("AllowedObjectIds").Get<string[]>() ?? Array.Empty<string>();
    });
}

var app = builder.Build();

app.MapGet("/healthz", () => Results.Ok(new
{
    Status = "Healthy",
    Service = "Reimaginate.DataHub.ReferenceHost"
}));

app.MapDataHubReadinessEndpoint("/readyz", options =>
{
    options.ServiceName = "Reimaginate.DataHub.ReferenceHost";
});

app.MapDataHubClientEndpoint("/api/Client", options =>
{
    if (string.Equals(clientAuthorizationMode, "AzureAdBearer", StringComparison.OrdinalIgnoreCase))
    {
        options.UseAzureAdBearerAuthorization();
        return;
    }

    options.AuthorizeAsync = (request, _) =>
    {
        var expectedKey = request.HttpContext.RequestServices
            .GetRequiredService<IConfiguration>()["x-functions-key"];
        var suppliedKey = request.Headers["x-functions-key"].FirstOrDefault();

        return Task.FromResult(!string.IsNullOrWhiteSpace(expectedKey) && expectedKey == suppliedKey);
    };
});

app.MapDataHubCliEndpoint("/api/CLI", options =>
{
    options.AuthenticateAsync = async (request, cancellationToken) =>
    {
        var config = request.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var enabled = config.GetValue<bool>("ReferenceHost:Cli:DevelopmentAuth:Enabled");
        if (!enabled)
        {
            return null;
        }

        var tenantHeader = config["ReferenceHost:Cli:DevelopmentAuth:TenantIdHeader"] ?? "x-datahub-dev-tenant-id";
        var userHeader = config["ReferenceHost:Cli:DevelopmentAuth:UserIdHeader"] ?? "x-datahub-dev-user-id";
        var tenantId = request.Headers[tenantHeader].FirstOrDefault();
        var userId = request.Headers[userHeader].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var mediator = request.HttpContext.RequestServices.GetRequiredService<IMediator>();
        var getUserResponse = (await mediator.TrySend<GetUserResponse>(
            new GetUserRequest
            {
                TenantId = tenantId,
                UserId = userId
            },
            cancellationToken)) switch { { Item2: { } exception } => throw exception, { Item1: var mediatorResultValue } => mediatorResultValue };

        return getUserResponse != null && getUserResponse.Success ? getUserResponse.Result : null;
    };
});

app.Run();
