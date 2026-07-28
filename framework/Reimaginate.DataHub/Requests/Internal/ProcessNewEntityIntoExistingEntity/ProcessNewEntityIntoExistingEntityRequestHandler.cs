using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.Helpers;
using Reimaginate.DataHub.Services.TimeService;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.DataHub.SharedModels.Rules;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Requests.Internal.ProcessNewEntityIntoExistingEntity;

public class ProcessNewEntityIntoExistingEntityRequestHandler(ITimeService timeService) : IHandler<ProcessNewEntityIntoExistingEntityRequest, ProcessNewEntityIntoExistingEntityResponse>
{
    public Task<ProcessNewEntityIntoExistingEntityResponse> HandleAsync(ProcessNewEntityIntoExistingEntityRequest request, CancellationToken cancellationToken)
    {
        var (fromEntity, toEntity, dataSource, sourceEntityType, sourceEntityId,
            entityConfig, toEntityLastUpdated, sourceEntityLastUpdated) = InitializeVars(request);

        var toEntityOriginalState = toEntity.DeepClone();
        AddNewEntityToExistingEntityAlternateKeys(dataSource, sourceEntityType, toEntity, sourceEntityId);

        var mergeRules = GetMergeRules(entityConfig, dataSource, sourceEntityType);
        if (mergeRules != null)
        {
            MergeUsingMergeRules(fromEntity, mergeRules, sourceEntityLastUpdated, toEntityLastUpdated, toEntity);
        }
        else
        {
            MergeUsingMostRecentlyUpdatedWins(sourceEntityLastUpdated, toEntityLastUpdated, fromEntity, toEntity);
        }

        var entityDiffs = GetUpdatesToExistingEntity(toEntityOriginalState, toEntity);

        return Task.FromResult(new ProcessNewEntityIntoExistingEntityResponse()
        {
            ResultingEntity = toEntity,
            EntityUpdates = entityDiffs is { HasValues: true } ? entityDiffs : null
        });
    }

    #region Private helpers

    private void MergeUsingMostRecentlyUpdatedWins(DateTimeOffset sourceEntityLastUpdated, DateTimeOffset toEntityLastUpdated, JObject fromEntity, JObject toEntity)
    {
        if (sourceEntityLastUpdated >= toEntityLastUpdated)
        {
            var patch = (JObject)fromEntity.DeepClone();
            typeof(DataHubEntityBase).GetProperties().ToList().ForEach(p => patch.Remove(p.Name));

            toEntity.Merge(patch, new JsonMergeSettings()
            {
                MergeArrayHandling = MergeArrayHandling.Replace,
                MergeNullValueHandling = MergeNullValueHandling.Ignore
            });
        }
    }

    private void MergeUsingMergeRules(JObject fromEntity, MergeRule mergeRules, DateTimeOffset sourceEntityLastUpdated, DateTimeOffset toEntityLastUpdated, JObject toEntity)
    {
        var patch = new JObject();
       
        var props =  fromEntity.GetAllProperties();
      
        foreach (var prop in props)
        {
            var propertyMergeRule = GetPropertyMergeRule(mergeRules, prop.Key);
            if (propertyMergeRule != null)
            {
                switch (propertyMergeRule.Action)
                {
                    case PropertyMergeRuleActions.AlwaysOverwrite:
                        patch.SetProperty(prop.Key, prop.Value);
                        break;

                    case PropertyMergeRuleActions.OverwriteIfNewer:
                        if (sourceEntityLastUpdated >= toEntityLastUpdated)
                        {
                            patch.SetProperty(prop.Key, prop.Value);
                        }
                        break;

                    case PropertyMergeRuleActions.OverwriteIfEmpty:
                        var toProp = toEntity.SelectToken(prop.Key, false);
                        if (prop.Value.Type != JTokenType.Null && IsEmptyValue(toProp))
                        {
                            patch.SetProperty(prop.Key, prop.Value);
                        }
                        break;

                    case PropertyMergeRuleActions.OverwriteIfNotEmpty:
                        if (prop.Value.Type != JTokenType.Null)
                        {
                            patch.SetProperty(prop.Key, prop.Value);
                        }
                        break;

                    case PropertyMergeRuleActions.NeverOverwrite:
                    case PropertyMergeRuleActions.DoNotUpdate:
                        // No action required
                        break;

                    default:
                        throw new ArgumentOutOfRangeException($"{propertyMergeRule.Action} is invalid");
                }
            }
        }

        toEntity.Merge(patch, new JsonMergeSettings()
        {
            MergeArrayHandling = MergeArrayHandling.Union,
            MergeNullValueHandling = MergeNullValueHandling.Merge
        });
    }

    private MergeRule GetMergeRules(EntityConfig entityConfig, string dataSource, string sourceEntityType)
    {
        var result = entityConfig.MergeRules?
            .Where(x => (string.Equals(x.DataSource, dataSource, StringComparison.CurrentCultureIgnoreCase) || x.DataSource == "*")
                        && (string.Equals(x.SourceEntityType, sourceEntityType, StringComparison.CurrentCultureIgnoreCase) || x.SourceEntityType == "*")
                        && (string.Equals(x.Context, MergeContexts.FirstTimeMerge, StringComparison.CurrentCultureIgnoreCase) || x.Context == "*" || string.IsNullOrEmpty(x.Context)))
            .OrderByDescending(x => SpecificityScore(x, dataSource, sourceEntityType, MergeContexts.FirstTimeMerge))
            .FirstOrDefault();
        return result;
    }

    private static int SpecificityScore(MergeRule rule, string dataSource, string sourceEntityType, string context)
    {
        var score = 0;
        if (string.Equals(rule.DataSource, dataSource, StringComparison.CurrentCultureIgnoreCase)) score += 4;
        if (string.Equals(rule.SourceEntityType, sourceEntityType, StringComparison.CurrentCultureIgnoreCase)) score += 2;
        if (string.Equals(rule.Context, context, StringComparison.CurrentCultureIgnoreCase)) score += 1;
        return score;
    }

    private static PropertyMergeRule GetPropertyMergeRule(MergeRule mergeRules, string propertyName)
    {
        return mergeRules.Rules.FirstOrDefault(w => string.Equals(w.PropertyName, propertyName, StringComparison.CurrentCultureIgnoreCase))
               ?? mergeRules.Rules.FirstOrDefault(w => w.PropertyName == "*");
    }

    private static bool IsEmptyValue(JToken token)
    {
        if (token == null || token.Type is JTokenType.Null or JTokenType.Undefined) return true;

        if (token.Type == JTokenType.String) return string.IsNullOrEmpty(token.Value<string>());
        if (token is JArray array) return array.Count == 0;
        if (token is JObject obj) return !obj.Properties().Any();

        if (token is JValue valueToken)
        {
            var value = valueToken.Value;
            return value != null && value.Equals(Activator.CreateInstance(value.GetType()));
        }

        return false;
    }

    private JObject GetUpdatesToExistingEntity(JToken toEntityOriginalState, JObject toEntity)
    {
        var entityDiffs = ChangeTrackingHelper.StripBaseProperties((JObject)ChangeTrackingHelper.DiffIgnoringEquivalentValueTokens(toEntityOriginalState, toEntity));
        return entityDiffs;
    }

    private void AddNewEntityToExistingEntityAlternateKeys(string dataSource, string sourceEntityType, JObject toEntity, string sourceEntityId)
    {
        var altKeyKey = $"{dataSource}.{sourceEntityType}".ToLower();
        var toEntityAltKeys = (JArray)toEntity[nameof(DataHubEntity.alternateKeys)]!;
        if(toEntityAltKeys == null) {
            toEntityAltKeys = JArray.FromObject(new List<AlternateKey>());
            toEntity.Add(nameof(DataHubEntity.alternateKeys), toEntityAltKeys);
        }
        
        var sourceSystemAltKey = new AlternateKey(altKeyKey, sourceEntityId)
        {
            LastMerge = timeService.Now()
        };

        toEntityAltKeys.Add(JToken.FromObject(sourceSystemAltKey));
    }

    private (JObject fromEntity, JObject toEntity, string dataSource, string sourceEntityType, string sourceEntityId, EntityConfig entityConfig, DateTimeOffset toEntityLastUpdated, DateTimeOffset sourceEntityLastUpdated) InitializeVars(ProcessNewEntityIntoExistingEntityRequest request)
    {
        var fromEntity = request.FromEntity;
        var toEntity = request.ToEntity;

        var dataSource = request.DataSource;
        var sourceEntityType = request.SourceEntityType;
        var sourceEntityId = request.SourceEntityId;
        var entityConfig = request.EntityConfig;

        var toEntityLastUpdated = GetMergeTimestamp(toEntity);
        var sourceEntityLastUpdated = GetMergeTimestamp(fromEntity);

        return (fromEntity, toEntity, dataSource, sourceEntityType, sourceEntityId, entityConfig, toEntityLastUpdated, sourceEntityLastUpdated);
    }

    private static DateTimeOffset GetMergeTimestamp(JObject entity)
    {
        return entity.DateTimeOffsetValue(nameof(DataHubEntity.lastUpdated))
               ?? entity.DateTimeOffsetValue(nameof(DataHubEntity.createdOn))
               ?? default;
    }

    #endregion
}
