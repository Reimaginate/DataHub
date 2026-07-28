using System;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Reimaginate.DataHub.AspNetCore;

public static class DataHubCliAzureAdAuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddDataHubCliAzureAdAuthentication(
        this IServiceCollection services,
        Action<DataHubCliAzureAdOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new DataHubCliAzureAdOptions();
        configure(options);
        Validate(options);

        services.AddSingleton(Options.Create(options));
        services.AddAuthentication()
            .AddJwtBearer(DataHubCliEndpointAuthenticationDefaults.AzureAdBearerScheme, jwtOptions =>
            {
                jwtOptions.MapInboundClaims = false;
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

    private static void Validate(DataHubCliAzureAdOptions options)
    {
        EnsureConfigured(options.TenantId, nameof(DataHubCliAzureAdOptions.TenantId));
        EnsureConfigured(options.Audience, nameof(DataHubCliAzureAdOptions.Audience));
        EnsureConfigured(options.RequiredScope, nameof(DataHubCliAzureAdOptions.RequiredScope));
    }

    private static void EnsureConfigured(string value, string optionName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"DataHubCliAzureAdOptions.{optionName} must be configured for DataHub CLI Azure AD authentication.");
        }
    }
}
