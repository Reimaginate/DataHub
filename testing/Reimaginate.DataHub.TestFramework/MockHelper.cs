using System.Reflection;
using Newtonsoft.Json.Linq;
using NSubstitute;
using NSubstitute.Core;

namespace Reimaginate.DataHub.TestFramework;

public static class MockHelper
{
    public static void MockBase<TInterface, TImplementationClass>(TInterface @interface, TImplementationClass implementationClass) where TInterface : class where TImplementationClass : class
    {
        var dataServiceSubstituteType = @interface.GetType();
        var nSubstituteArgBaseMethod = typeof(Arg).GetMethod(nameof(Arg.Any))!;

        var interfaceMethods = dataServiceSubstituteType.GetInterfaceMap(typeof(TInterface));

        var substituteDataServiceMethods = dataServiceSubstituteType.GetMethods().Where(w => interfaceMethods.TargetMethods.Contains(w) && !w.IsGenericMethod).ToList();
        foreach (var substituteMethod in substituteDataServiceMethods)
        {
            var dataServiceMethodArgs = substituteMethod.GetParameters().ToList();
            var substituteArgs = dataServiceMethodArgs.Select(s =>
            {
                var paramType = s.ParameterType;
                if (paramType.ContainsGenericParameters) paramType = paramType.BaseType;
                return nSubstituteArgBaseMethod.MakeGenericMethod(paramType!).Invoke(null, null);
            }).ToArray();

            var buildExpressionMethod = typeof(MockHelper).GetMethods().First(f => f.Name == nameof(BuildReturnsForAnyArgsExpression));
            var buildExpressionMethodAsGeneric = buildExpressionMethod.MakeGenericMethod(substituteMethod.ReturnType, typeof(TImplementationClass));

            var mockExpression = buildExpressionMethodAsGeneric.Invoke(substituteMethod, new object[] { implementationClass, substituteMethod });

            var returnsForAnyArgsMethodInfo = typeof(SubstituteExtensions).GetMethods().Where(w => w.Name == nameof(SubstituteExtensions.ReturnsForAnyArgs)).ToList()[1]
                .MakeGenericMethod(substituteMethod.ReturnType);

            var result = substituteMethod.Invoke(@interface, substituteArgs);
            returnsForAnyArgsMethodInfo.Invoke(null, new[] { result, mockExpression, null });
        }
    }

    public static Func<CallInfo, T> BuildReturnsForAnyArgsExpression<T, TImplementationClass>(TImplementationClass realDataService, MethodInfo substituteMethod) where TImplementationClass : class
    {
        return ci =>
        {
            var realMethods = realDataService.GetType().GetMethods();
            var realMethod = realMethods.First(w => w.ContainsGenericParameters == substituteMethod.ContainsGenericParameters
                                                    && w.IsGenericMethod == substituteMethod.IsGenericMethod
                                                    && w.Name == substituteMethod.Name
                                                    && JToken.DeepEquals(
                                                        JArray.FromObject(substituteMethod.GetParameters().Select(s => s.ParameterType).ToList()),
                                                        JArray.FromObject(w.GetParameters().Select(s => s.ParameterType).ToList())));
            var result = realMethod!.Invoke(realDataService, ci.Args());
            return (T)result!;
        };
    }

}