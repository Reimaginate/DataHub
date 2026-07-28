using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Reimaginate.DataHub.SharedModels.Core;

namespace Reimaginate.DataHub.TestFramework.Helpers;

public static class ServiceProviderExtensions
{
    public static void Replace<TInterface, TOldImplementation, TNewImplementation>(this IServiceCollection services) where TInterface : class where TOldImplementation : class where TNewImplementation : class
    {
        services.Remove(new ServiceDescriptor(typeof(TInterface), typeof(TOldImplementation)));
        services.AddScoped(typeof(TInterface), typeof(TNewImplementation));
    }

    private static TInterface InitializeMock<TInterface>(IServiceProvider serviceProvider)
        where TInterface : class
    {
        var mock = Substitute.For<TInterface>();

        var tInterfaceMembers = typeof(TInterface).GetInterfaces()
            .Concat(new[] { typeof(TInterface) })
            .SelectMany(i => i.GetMembers())
            .Distinct()
            .ToArray();

        var dataHubAssembly = typeof(ChangeTrackingEntry).Assembly;
        var dataHubSharedAssembly = typeof(CosmosDocument).Assembly;
        var cosmosDocumentTypes = dataHubAssembly.ExportedTypes.Where(type => type.BaseType == typeof(CosmosDocument)).ToList();
        cosmosDocumentTypes.AddRange(dataHubSharedAssembly.ExportedTypes.Where(type => type.BaseType == typeof(CosmosDocument)).ToList());

        foreach (var tInterfaceMember in tInterfaceMembers)
        {
            if (tInterfaceMember.MemberType != MemberTypes.Method) continue;

            var tInterfaceMethod = tInterfaceMember as MethodInfo;
            if (tInterfaceMethod == null || tInterfaceMethod.IsSpecialName) continue;

            var tInterfaceMemberReturnType = tInterfaceMethod.ReturnType;
            if (tInterfaceMemberReturnType.BaseType != typeof(Task)) continue;

            var returnType = tInterfaceMemberReturnType.GetGenericArguments()[0];
            var methodParams = tInterfaceMethod.GetParameters();
            var invokeArgs = new object?[methodParams.Length];
            for (var i = 0; i < invokeArgs.Length; i++)
            {
                invokeArgs[i] = default;
            }

            if (tInterfaceMethod.ContainsGenericParameters)
            {
                foreach (var cosmosDocumentType in cosmosDocumentTypes)
                {
                    var genericMethodDef = tInterfaceMethod.GetGenericMethodDefinition();
                    var genericArguments = genericMethodDef.GetGenericArguments();
                    var constraints = genericArguments.FirstOrDefault()?.GetGenericParameterConstraints();
                    if (constraints?.Any() ?? false)
                    {
                        var isAllowedType = cosmosDocumentType.IsAssignableFrom(constraints.First());
                        if (!isAllowedType) continue;
                    }

                    var genericMethodInfo = genericMethodDef.MakeGenericMethod(cosmosDocumentType);
                    var t = genericMethodInfo.Invoke(mock, invokeArgs);
                    t.ReturnsForAnyArgs(x =>
                    {
                        if (returnType.ContainsGenericParameters)
                        {
                            var returnTypeGenericArgs = returnType.GetGenericArguments();
                            var concreteArguments = Array.ConvertAll(returnTypeGenericArgs, _ => cosmosDocumentType);

                            var genericTypeDef = returnType.GetGenericTypeDefinition();
                            returnType = genericTypeDef.MakeGenericType(concreteArguments);
                        }

                        var returnValue = Activator.CreateInstance(returnType);
                        var fromResultMethod = typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(returnType);
                        var returnTask = fromResultMethod.Invoke(null, new[] { returnValue });

                        var callHistory = serviceProvider.GetRequiredService<ServiceCallHistory>();
                        callHistory.ServiceCalls.Add(new ServiceCall()
                        {
                            Mock = mock,
                            Method = tInterfaceMethod,
                            MethodName = tInterfaceMethod.Name,
                            Args = x.Args(),
                            Response = returnValue
                        });

                        return returnTask;
                    });
                }
            }
            else
            {
                var t = tInterfaceMethod.Invoke(mock, invokeArgs);
                t.ReturnsForAnyArgs(x =>
                {
                    if (returnType.ContainsGenericParameters)
                    {
                        var genericArguments = returnType.GetGenericArguments();
                        var concreteArguments = Array.ConvertAll(genericArguments, _ => typeof(object));
                        returnType = returnType.GetGenericTypeDefinition().MakeGenericType(concreteArguments);
                    }

                    var returnValue = Activator.CreateInstance(returnType);
                    var fromResultMethod = typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(returnType);
                    var returnTask = fromResultMethod.Invoke(null, new[] { returnValue });

                    var callHistory = serviceProvider.GetRequiredService<ServiceCallHistory>();
                    callHistory.ServiceCalls.Add(new ServiceCall()
                    {
                        Mock = mock,
                        Method = tInterfaceMethod,
                        MethodName = tInterfaceMethod.Name,
                        Args = x.Args(),
                        Response = returnValue
                    });

                    return returnTask;
                });
            }
        }

        return mock;
    }

    public static void ReplaceDataServiceWithMock<TInterface, TImplementation>(this IServiceCollection services)
        where TInterface : class
        where TImplementation : TInterface
    {


        services.Remove(new ServiceDescriptor(typeof(TInterface), typeof(TImplementation)));
        services.AddSingleton(InitializeMock<TInterface>);
    }
}
