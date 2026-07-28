using Reimaginate.DataHub.CLI.Tools.PluginBase;

namespace Reimaginate.DataHub.CLI.Tools.Commands.Register;

public class RegisterCommand(IServiceProvider serviceProvider) : TopLevelCommand("register", serviceProvider);