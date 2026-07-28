using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.TestFramework.Helpers;

public static class Where
{
    public static string NameEquals(string value) => $"x.Name = '{value}'";

    public static string NameIn(IEnumerable<string> values) => $"x.Name in ({string.Join(",", values.Select(s => $"'{s}'"))})";

    public static string EmailEquals(string value) => $"x.Email = '{value}'";

    public static string EmailIn(IEnumerable<string> values) => $"x.Email in ({string.Join(",", values.Select(s => $"'{s}'"))})";

    public static string DescriptionEquals(string value) => $"x.Description = '{value}'";

    public static string AlternateKeyFound(AlternateKey value) => $"exists (select ak from ak in x.alternateKeys where ak['Key'] = '{value.Key}' and ak['Value'] = '{value.Value}')";

    public static string AlternateKeysFound(string key, List<string> values) =>
        $"exists (select ak from ak in x.alternateKeys where ak['Key'] = '{key}' and ak['Value'] in ({string.Join(",", values.Select(s => $"'{s}'"))}))";
}
