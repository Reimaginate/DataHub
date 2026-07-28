namespace Reimaginate.DataHub.SharedModels.Core;

public class AzureFunctionDefinition
{
    public string Name { get; set; }
    public FunctionConfig Config { get; set; }
}

public class FunctionConfig
{
    public FunctionBinding[] Bindings { get; set; }
}

public class FunctionBinding
{
    public string Type { get; set; }
}