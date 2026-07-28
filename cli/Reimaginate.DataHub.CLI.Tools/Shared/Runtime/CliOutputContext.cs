namespace Reimaginate.DataHub.CLI.Tools.Shared.Runtime;

public enum CliOutputFormat
{
    Table,
    Json,
    Ndjson,
    Tsv
}

public static class CliOutputContext
{
    private static readonly AsyncLocal<CliOutputFormat> CurrentFormat = new();

    static CliOutputContext()
    {
        CurrentFormat.Value = CliOutputFormat.Table;
    }

    public static CliOutputFormat Format
    {
        get => CurrentFormat.Value;
        set => CurrentFormat.Value = value;
    }
}
