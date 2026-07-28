using System.Collections.Generic;
using Newtonsoft.Json;

namespace Reimaginate.DataHub.Helpers;

public static class ObjectExtensions
{
    public static Dictionary<string, object> ToValuesDictionary(this object o, List<string> ignoreProps = null)
    {
        var t = o.GetType();
        var props = t.GetProperties();

        var ret = new Dictionary<string, object>();
        foreach (var prop in props)
        {
            if (ignoreProps == null || ignoreProps.Contains(prop.Name)) continue;
            var val = prop.GetValue(o);
            ret.Add(prop.Name, val);
        }

        return ret;
    }

    public static Dictionary<string, string> ToPropsDictionary(this object o, string key)
    {
        var ret = new Dictionary<string, string>() { { key, o.ToString() } };
        return ret;
    }

    public static string Serialize(this object o)
    {
        return o != null ? JsonConvert.SerializeObject(o) : null;
    }
}