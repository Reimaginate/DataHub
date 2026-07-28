using Newtonsoft.Json.Linq;

namespace Reimaginate.DataHub.TestFramework.Helpers;

public static class JObjectExtensions
{
    public static JObject ExcludeProperties(this JObject originalObject, List<string> propertiesToExclude)
    {

        var modifiedObject = (JObject)originalObject.DeepClone();

        foreach (var propertyName in propertiesToExclude)
        {
            if (modifiedObject[propertyName] != null)
            {
                modifiedObject.Remove(propertyName);
            }
        }

        return modifiedObject;
    }
}