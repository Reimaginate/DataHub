namespace Reimaginate.DataHub.Templates;

public static class CodeGenerationTemplates
{
    public static class ClassTemplates
    {
        public static string DuplicateResolverTemplate = @"
                    using System;
                    using System.Collections.Generic;
                    using System.Globalization;
                    using System.Linq;
                    using System.Threading;
                    using System.Threading.Tasks;
                    using Newtonsoft.Json.Linq;
                    using Reimaginate.DataHub;
                    using Reimaginate.DataHub.DataAccess.Queries.FindMatchingEntities;                    
                    using Reimaginate.DataHub.Requests.Internal.FindDuplicate;
                    using Reimaginate.DataHub.SharedModels.Core;
                    using Reimaginate.Mediator;
                    using Reimaginate.DataHub.SharedModels.Core.Interfaces;

                    public class DuplicateResolver : IDuplicateResolver
                    {{

                        private bool IsNumericType(Type t)
                        {{
                            switch (Type.GetTypeCode(t))
                            {{
                                case TypeCode.Byte:
                                case TypeCode.Decimal:
                                case TypeCode.Double:
                                case TypeCode.Int16:
                                case TypeCode.Int32:
                                case TypeCode.Int64:
                                case TypeCode.SByte:
                                case TypeCode.Single:
                                case TypeCode.UInt16:
                                case TypeCode.UInt32:
                                case TypeCode.UInt64:
                                    return true;
                                case TypeCode.Object:
                                    if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>))
                                    {{
                                        return IsNumericType(Nullable.GetUnderlyingType(t));
                                    }}
                                    return false;
                            }}
                            return false;
                        }}

                        private bool IsNullOrUndefined(JToken token)
                        {{
                            return token == null || token.Type == JTokenType.Null || token.Type == JTokenType.Undefined;
                        }}

                        private bool Equals<T>(JToken pd, JToken i, string propName, bool includeNulls = false)
                        {{
                            var pdToken = pd?[propName];
                            var iToken = i?[propName];
                            if (!includeNulls && (IsNullOrUndefined(pdToken) || IsNullOrUndefined(iToken))) return false;
                            if (IsNullOrUndefined(pdToken) && IsNullOrUndefined(iToken)) return true;
                            if (IsNullOrUndefined(pdToken) || IsNullOrUndefined(iToken)) return false;

                            if (typeof(T) == typeof(string) || typeof(T) == typeof(char))
                            {{
                                var pdValue = Convert.ToString(pdToken.Value<T>(), CultureInfo.InvariantCulture);
                                var iValue = Convert.ToString(iToken.Value<T>(), CultureInfo.InvariantCulture);
                                return string.Equals(pdValue, iValue, StringComparison.OrdinalIgnoreCase);
                            }}

                            return EqualityComparer<T>.Default.Equals(pdToken.Value<T>(), iToken.Value<T>());
                        }}


                        private bool PathEquals<T>(JToken pd, JToken i, string propPath, bool includeNulls = false)
                        {{
                            var pdToken = pd.SelectToken(propPath);
                            var iToken = i.SelectToken(propPath);
                            if (!includeNulls && (IsNullOrUndefined(pdToken) || IsNullOrUndefined(iToken))) return false;
                            if (IsNullOrUndefined(pdToken) && IsNullOrUndefined(iToken)) return true;
                            if (IsNullOrUndefined(pdToken) || IsNullOrUndefined(iToken)) return false;

                            if (typeof(T) == typeof(string) || typeof(T) == typeof(char))
                            {{
                                var pdValue = Convert.ToString(pdToken.Value<T>(), CultureInfo.InvariantCulture);
                                var iValue = Convert.ToString(iToken.Value<T>(), CultureInfo.InvariantCulture);
                                return string.Equals(pdValue, iValue, StringComparison.OrdinalIgnoreCase);
                            }}

                            return EqualityComparer<T>.Default.Equals(pdToken.Value<T>(), iToken.Value<T>());
                        }}

                        private string In(List<JObject> i, string propName)
                        {{
                            return In<string>(i, propName);
                        }}

                        private string In<T>(List<JObject> i, string propName)
                        {{
                            return FormatInValues<T>(i.Select(s => s[propName]));
                        }}

                        private string PathIn(List<JObject> i, string propPath)
                        {{
                            return PathIn<string>(i, propPath);
                        }}

                        private string PathIn<T>(List<JObject> i, string propPath)
                        {{
                            return FormatInValues<T>(i.Select(s => s.SelectToken(propPath)));
                        }}

                        private string FormatInValues<T>(IEnumerable<JToken> tokens)
                        {{
                            var values = tokens
                                .Where(t => !IsNullOrUndefined(t))
                                .Select(FormatInValue<T>)
                                .Where(v => !string.IsNullOrWhiteSpace(v))
                                .ToList();

                            return values.Any() ? string.Join("","", values) : ""null"";
                        }}

                        private string FormatInValue<T>(JToken token)
                        {{
                            var value = token.Value<T>();
                            if (value == null) return null;

                            if (IsNumericType(typeof(T)))
                            {{
                                return Convert.ToString(value, CultureInfo.InvariantCulture);
                            }}

                            return $""'{{EscapeSqlString(Convert.ToString(value, CultureInfo.InvariantCulture)).ToLowerInvariant()}}'"";
                        }}

                        private string EscapeSqlString(string value)
                        {{
                            return value?.Replace(""'"", ""\\'"");
                        }}

                        public async Task<JArray> FindPotentialDuplicatesAsync(IMediator mediator, string dataHubEntityType, List<JObject> i, CancellationToken cancellationToken)
                        {{
                            var findMatchingEntitiesQuery = new FindMatchingEntitiesQuery()
                            {{
                                EntityType = dataHubEntityType,
                                WhereClause = $""{0}""
                            }};

                            var results = (await mediator.TrySend<JArray>(findMatchingEntitiesQuery, cancellationToken)) switch {{ {{ Item2: {{ }} exception }} => throw exception, {{ Item1: var mediatorResultValue }} => mediatorResultValue }};

                            return results;
                        }}

                        public IEnumerable<JToken> Resolve(JObject i, JArray potentialDuplicates)
                        {{
                            bool Predicate(JToken pd) => {1};
                            return potentialDuplicates.Where(Predicate);
                        }}
                    }}";

        public static string PreMergeRuleResolverTemplate = @"
                    using System;
                    using System.Collections.Generic;
                    using System.Linq;
                    using System.Threading;
                    using System.Threading.Tasks;
                    using Newtonsoft.Json.Linq;
                    using Reimaginate.DataHub.SharedModels.Core;
                    using Reimaginate.DataHub.SharedModels.Core.Interfaces;

                    public class PreMergeRuleResolver : IPreMergeRuleResolver
                    {{
                          public bool Resolve(JObject incoming, JObject existing, ChangeTrackingEntry changeSet) => {0};                          
                    }}";
    }
}
