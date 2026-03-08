namespace SupplierStatementReader.Models;

public sealed class AppOptions
{
    public AwsOptions Aws { get; set; } = new();
    public OutputOptions Output { get; set; } = new();
    public TextractOptions Textract { get; set; } = new();
}

public sealed class AwsOptions
{
    public string Region { get; set; } = "eu-west-1";
}

public sealed class OutputOptions
{
    public string Folder { get; set; } = string.Empty;
}

public sealed class TextractOptions
{
    public string StagingBucket { get; set; } = string.Empty;
    public string StagingPrefix { get; set; } = "supplier-statements";
    public int PollDelaySeconds { get; set; } = 2;
    public int PollTimeoutSeconds { get; set; } = 120;
}
