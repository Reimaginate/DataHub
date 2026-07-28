using System;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Reimaginate.DataHub.AspNetCore;

public static class DataHubClientAzureAdAuthorizationServiceCollectionExtensions
{
    public static IServiceCollection AddDataHubClientAzureAdAuthorization(
        this IServiceCollection services,
        Action<DataHubClientAzureAdOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new DataHubClientAzureAdOptions();
        configure(options);
        Validate(options);

        services.AddSingleton(Options.Create(options));
        services.AddAuthentication()
            .AddJwtBearer(DataHubClientEndpointAuthenticationDefaults.AzureAdBearerScheme, jwtOptions =>
            {
                jwtOptions.Authority = $"https://login.microsoftonline.com/{options.TenantId}/v2.0";
                jwtOptions.Audience = options.Audience;
                jwtOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidAudience = options.Audience,
                    ValidateLifetime = true
                };
            });

        return services;
    }

    private static void Validate(DataHubClientAzureAdOptions options)
    {
        EnsureConfigured(options.TenantId, nameof(DataHubClientAzureAdOptions.TenantId));
        EnsureConfigured(options.Audience, nameof(DataHubClientAzureAdOptions.Audience));

        if ((options.AllowedClientIds == null || options.AllowedClientIds.Length == 0)
            && (options.AllowedObjectIds == null || options.AllowedObjectIds.Length == 0))
        {
            throw new InvalidOperationException(
                $"At least one {nameof(DataHubClientAzureAdOptions.AllowedClientIds)} or {nameof(DataHubClientAzureAdOptions.AllowedObjectIds)} value must be configured for DataHub client Azure AD authorization.");
        }
    }

    private static void EnsureConfigured(string value, string optionName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"DataHubClientAzureAdOptions.{optionName} must be configured for DataHub client Azure AD authorization.");
        }
    }
}
