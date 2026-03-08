using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SupplierStatementReader.Models;

namespace SupplierStatementReader.Services;

public sealed class JsonOutputService
{
    private readonly ILogger<JsonOutputService> _logger;
    private readonly AppOptions _options;

    public JsonOutputService(ILogger<JsonOutputService> logger, IOptions<AppOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public async Task<string> WriteAsync(string sourcePdfPath, StatementResult result, CancellationToken cancellationToken)
    {
        var outputDirectory = string.IsNullOrWhiteSpace(_options.Output.Folder)
            ? Path.GetDirectoryName(sourcePdfPath) ?? Directory.GetCurrentDirectory()
            : _options.Output.Folder;

        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(outputDirectory, $"{Path.GetFileNameWithoutExtension(sourcePdfPath)}.json");

        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(outputPath, json, cancellationToken);

        _logger.LogInformation("Output written: {Path}", outputPath);
        return json;
    }
}
