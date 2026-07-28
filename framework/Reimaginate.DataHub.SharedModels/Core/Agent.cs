namespace Reimaginate.DataHub.SharedModels.Core;

public class Agent
{
    public string Name { get; set; }

    public string Description { get; set; }

    public string Type { get; set; }

    public string ControllerUrl { get; set; }

    public string ControllerKey { get; set; }

    public string DurableTaskExtensionKey { get; set; }
}