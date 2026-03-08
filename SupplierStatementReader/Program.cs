using Amazon;
using Amazon.S3;
using Amazon.Textract;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SupplierStatementReader.Models;
using SupplierStatementReader.Parsing;
using SupplierStatementReader.Services;

namespace SupplierStatementReader;

public static class Program
{
    public static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Please provide a PDF file path or folder path.");
            return;
        }

        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        var options = config.Get<AppOptions>() ?? new AppOptions();
        var region = RegionEndpoint.GetBySystemName(options.Aws.Region);

        var services = new ServiceCollection()
            .AddLogging(builder => builder.AddSimpleConsole(c => c.TimestampFormat = "HH:mm:ss "))
            .AddSingleton(Microsoft.Extensions.Options.Options.Create(options))
            .AddSingleton<IAmazonTextract>(_ => new AmazonTextractClient(region))
            .AddSingleton<IAmazonS3>(_ => new AmazonS3Client(region))
            .AddSingleton<TextractService>()
            .AddSingleton<HeaderExtractor>()
            .AddSingleton<InvoiceTableDetector>()
            .AddSingleton<InvoiceRowMapper>()
            .AddSingleton<SanitizationService>()
            .AddSingleton<StatementParser>()
            .AddSingleton<JsonOutputService>()
            .BuildServiceProvider();

        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Main");
        var textractService = services.GetRequiredService<TextractService>();
        var parser = services.GetRequiredService<StatementParser>();
        var output = services.GetRequiredService<JsonOutputService>();

        var input = args[0];
        var files = ResolveInputFiles(input).ToList();
        if (files.Count == 0)
        {
            logger.LogError("No PDF files found for path: {Input}", input);
            return;
        }

        foreach (var file in files)
        {
            try
            {
                logger.LogInformation("File started: {File}", file);
                logger.LogInformation("Textract request started: {File}", file);
                var doc = await textractService.AnalyzePdfAsync(file, CancellationToken.None);
                logger.LogInformation("Textract request completed: {File}", file);

                var result = parser.Parse(doc);
                logger.LogInformation("Number of invoice rows accepted: {Count}", result.InvoiceList.Count);

                var json = await output.WriteAsync(file, result, CancellationToken.None);
                Console.WriteLine(json);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Per-file failure: {File}", file);
            }
        }
    }

    private static IEnumerable<string> ResolveInputFiles(string input)
    {
        if (File.Exists(input))
        {
            if (Path.GetExtension(input).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                yield return input;
            }
            yield break;
        }

        if (Directory.Exists(input))
        {
            foreach (var file in Directory.EnumerateFiles(input, "*.pdf", SearchOption.TopDirectoryOnly))
            {
                yield return file;
            }
            yield break;
        }

        throw new FileNotFoundException($"Input path does not exist: {input}");
    }
}
