using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Reimaginate.DataHub.Services.DynamicAssemblies;
using Reimaginate.DataHub.Services.EntityConfig;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core.Interfaces;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.CheckPreMergeRules;

public class CheckPreMergeRulesRequestHandler(IServiceProvider serviceProvider) : IHandler<CheckPreMergeRulesRequest, CheckPreMergeRulesResponse>
{
    private readonly IDynamicAssembliesService _dynamicAssembliesService = serviceProvider.GetRequiredService<IDynamicAssembliesService>();
    private readonly IEntityConfigService _entityConfigService = serviceProvider.GetRequiredService<IEntityConfigService>();

    public async Task<CheckPreMergeRulesResponse> HandleAsync(CheckPreMergeRulesRequest request, CancellationToken cancellationToken)
    {
        var entityConfig = request.EntityConfig ?? await _entityConfigService.GetEntityConfig(request.DataHubEntityType, cancellationToken);

        if (!(entityConfig?.PreMergeRules.Any() ?? false))
            return new CheckPreMergeRulesResponse()
            {
                Pass = true
            };


        var applicableRules = entityConfig.PreMergeRules.Where(x =>
            (x.DataSource == request.DataSource || x.DataSource == "*")
            && (x.SourceEntityType == request.SourceEntityType || x.SourceEntityType == "*")
            && (x.Context == request.Context || x.Context == "*")
        ).ToList();

        foreach (var rule in applicableRules)
        {
            if (string.IsNullOrEmpty(rule.Predicate))
            {
                if (rule.IfTrue == PreMergeRuleActions.AllowMerge)
                {
                    return new CheckPreMergeRulesResponse()
                    {
                        Pass = true
                    };
                }

                return new CheckPreMergeRulesResponse()
                {
                    Pass = false,
                    Action = rule.IfTrue
                };
            }

            var ruleIndex = entityConfig.PreMergeRules.IndexOf(rule);
            var resolverName = $"PreMergeRuleResolver_{entityConfig.id}_{ruleIndex}";
            var resolver = _dynamicAssembliesService.LoadAssemblyAndReturnType<IPreMergeRuleResolver>(DynamicAssemblyTypes.PreMergeRuleResolver, resolverName, [rule.Predicate], "PreMergeRuleResolver");
            var isTrue = resolver.Resolve(request.IncomingEntity, request.ExistingEntity, request.ChangeSet);

            if (isTrue) return rule.IfTrue switch
            {
                PreMergeRuleActions.AllowMerge => new CheckPreMergeRulesResponse() { Pass = false, Action = rule.IfTrue },
                _ => new CheckPreMergeRulesResponse() { Pass = false, Action = rule.IfTrue }
            };
        }

        return new CheckPreMergeRulesResponse()
        {
            Pass = true
        };
    }
}