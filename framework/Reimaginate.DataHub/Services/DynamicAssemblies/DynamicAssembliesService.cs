
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;


namespace Reimaginate.DataHub.Services.DynamicAssemblies;

public interface IDynamicAssembliesService : IDisposable
{
    void AddAssembly(string assemblyName, Assembly assembly, int maxInactiveMinutes = 5);
    Assembly LoadAssembly(DynamicAssemblyTypes assemblyType, string key, object[] args, int maxInactiveMinutes = 5);
    T LoadAssemblyAndReturnType<T>(DynamicAssemblyTypes assemblyType, string key, object[] args, string typeName, int maxInactiveMinutes = 5);
}

public enum DynamicAssemblyTypes { DuplicateResolver, PreMergeRuleResolver }

public class DynamicAssembliesService : IDynamicAssembliesService
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    public DynamicAssembliesService()
    { }

    public void AddAssembly(string key, Assembly assembly, int maxInactiveMinutes = 2)
    {
        var cacheOptions = new MemoryCacheEntryOptions().SetPriority(CacheItemPriority.NeverRemove);
        _cache.Set(key, assembly, cacheOptions);
    }

    public Assembly LoadAssembly(DynamicAssemblyTypes assemblyType, string key, object[] args, int maxInactiveMinutes = 5)
    {
        var cacheKey = $"{assemblyType}:{key}:{string.Join("|", args.Select(a => a?.ToString() ?? string.Empty))}";
        if (_cache.TryGetValue(cacheKey, out Assembly cachedAssembly))
        {
            return cachedAssembly;
        }

        var assembly = _assemblyBuilders[assemblyType](args);
        AddAssembly(cacheKey, assembly);
        return assembly;
    }

    public T LoadAssemblyAndReturnType<T>(DynamicAssemblyTypes assemblyType, string key, object[] args, string typeName, int maxInactiveMinutes = 5)
    {
        var assembly = LoadAssembly(assemblyType, key, args, maxInactiveMinutes);

        var type = assembly.GetType(typeName);
        var typeInstance = Activator.CreateInstance(type!);
        return (T)typeInstance;
    }

    private readonly Dictionary<DynamicAssemblyTypes, Func<object[], Assembly>> _assemblyBuilders = new()
    {
        {DynamicAssemblyTypes.DuplicateResolver, (args) =>
        {
            var code = string.Format(Templates.CodeGenerationTemplates.ClassTemplates.DuplicateResolverTemplate, args);
            return BuildAssembly(code);
        }},

        { DynamicAssemblyTypes.PreMergeRuleResolver, (args) =>
        {
            var code = string.Format(Templates.CodeGenerationTemplates.ClassTemplates.PreMergeRuleResolverTemplate, args);
            return BuildAssembly(code);
        } }
    };

    private static Assembly BuildAssembly(string code)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(code);

        var basePath = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var assemblyPaths = new List<string>
        {
            Path.Combine(basePath, "System.ObjectModel.dll"),
            Path.Combine(basePath, "System.Runtime.dll"),
            Path.Combine(basePath, "System.ObjectModel.dll"),
            typeof(object).Assembly.Location,
            typeof(string).Assembly.Location,
            typeof(System.ComponentModel.TypeConverter).Assembly.Location,
            typeof(Config.DataHubOptions).Assembly.Location
        };

        foreach (var referencedAssemblyName in typeof(Config.DependencyInjection).Assembly.GetReferencedAssemblies())
        {
            var referencedAssembly = Assembly.Load(referencedAssemblyName);
            assemblyPaths.Add(referencedAssembly.Location);
        }

        var metadataReferences = assemblyPaths.Distinct().Select(filePath => MetadataReference.CreateFromFile(filePath)).ToList();

        var compilation = CSharpCompilation.Create("DynamicAssembly")
            .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release))
            .WithReferences(metadataReferences)
            .AddSyntaxTrees(syntaxTree)
            ;

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);

        if (!result.Success)
        {
            var failures = result.Diagnostics.Where(diagnostic =>
                diagnostic.IsWarningAsError ||
                diagnostic.Severity == DiagnosticSeverity.Error);

            throw new AggregateException("Could not compile assembly", failures.Select(f => new Exception(f.Id, new Exception(f.GetMessage()))));
        }

        ms.Seek(0, SeekOrigin.Begin);

        var asm = Assembly.Load(ms.ToArray());

        return asm;
    }

    public void Dispose()
    {
        _cache?.Dispose();
    }
}
