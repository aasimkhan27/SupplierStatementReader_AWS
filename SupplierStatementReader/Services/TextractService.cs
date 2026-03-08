using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Textract;
using Amazon.Textract.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SupplierStatementReader.Models;
using SupplierStatementReader.Textract;

namespace SupplierStatementReader.Services;

public sealed class TextractService
{
    private readonly IAmazonTextract _textract;
    private readonly IAmazonS3 _s3;
    private readonly AppOptions _options;
    private readonly ILogger<TextractService> _logger;

    public TextractService(IAmazonTextract textract, IAmazonS3 s3, IOptions<AppOptions> options, ILogger<TextractService> logger)
    {
        _textract = textract;
        _s3 = s3;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<TextractDocumentModel> AnalyzePdfAsync(string pdfPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Textract.StagingBucket))
        {
            throw new InvalidOperationException("Textract.StagingBucket must be configured in appsettings.json for PDF processing.");
        }

        var objectKey = $"{_options.Textract.StagingPrefix.Trim('/')}/{Guid.NewGuid():N}-{Path.GetFileName(pdfPath)}";
        await using var stream = File.OpenRead(pdfPath);

        _logger.LogInformation("Uploading file to S3 staging bucket for Textract: s3://{Bucket}/{Key}", _options.Textract.StagingBucket, objectKey);
        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _options.Textract.StagingBucket,
            Key = objectKey,
            InputStream = stream,
            ContentType = "application/pdf"
        }, cancellationToken);

        var startResponse = await _textract.StartDocumentAnalysisAsync(new StartDocumentAnalysisRequest
        {
            DocumentLocation = new DocumentLocation
            {
                S3Object = new Amazon.Textract.Model.S3Object
                {
                    Bucket = _options.Textract.StagingBucket,
                    Name = objectKey
                }
            },
            FeatureTypes = [FeatureType.TABLES, FeatureType.FORMS]
        }, cancellationToken);

        var blocks = await PollAnalysisBlocksAsync(startResponse.JobId, cancellationToken);
        return BuildDocumentModel(blocks);
    }

    private async Task<List<Block>> PollAnalysisBlocksAsync(string jobId, CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        while (true)
        {
            var response = await _textract.GetDocumentAnalysisAsync(new GetDocumentAnalysisRequest
            {
                JobId = jobId
            }, cancellationToken);

            if (response.JobStatus is JobStatus.SUCCEEDED)
            {
                var allBlocks = new List<Block>(response.Blocks);
                var nextToken = response.NextToken;
                while (!string.IsNullOrWhiteSpace(nextToken))
                {
                    var paged = await _textract.GetDocumentAnalysisAsync(new GetDocumentAnalysisRequest
                    {
                        JobId = jobId,
                        NextToken = nextToken
                    }, cancellationToken);
                    allBlocks.AddRange(paged.Blocks);
                    nextToken = paged.NextToken;
                }
                return allBlocks;
            }

            if (response.JobStatus is JobStatus.FAILED or JobStatus.PARTIAL_SUCCESS)
            {
                throw new InvalidOperationException($"Textract analysis failed with status {response.JobStatus}.");
            }

            if ((DateTimeOffset.UtcNow - startedAt).TotalSeconds > _options.Textract.PollTimeoutSeconds)
            {
                throw new TimeoutException("Timed out waiting for Textract analysis job.");
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.Textract.PollDelaySeconds), cancellationToken);
        }
    }

    private static TextractDocumentModel BuildDocumentModel(List<Block> blocks)
    {
        var blockById = blocks.Where(b => !string.IsNullOrWhiteSpace(b.Id)).ToDictionary(b => b.Id!);
        var lines = blocks
            .Where(b => b.BlockType == BlockType.LINE)
            .Select(b => new PageTextLine { Page = b.Page ?? 1, Text = Normalize(b.Text) })
            .Where(l => !string.IsNullOrWhiteSpace(l.Text))
            .ToList();

        var keyValues = ExtractKeyValues(blocks, blockById);
        var tables = ExtractTables(blocks, blockById);

        return new TextractDocumentModel
        {
            Lines = lines,
            KeyValues = keyValues,
            Tables = tables
        };
    }

    private static Dictionary<string, KeyValueModel> ExtractKeyValues(List<Block> blocks, Dictionary<string, Block> blockById)
    {
        var dict = new Dictionary<string, KeyValueModel>(StringComparer.OrdinalIgnoreCase);
        foreach (var keyBlock in blocks.Where(b => b.BlockType == BlockType.KEY_VALUE_SET && b.EntityTypes.Contains(EntityType.KEY)))
        {
            var keyText = GetChildText(keyBlock, blockById);
            var valueRelationship = keyBlock.Relationships?.FirstOrDefault(r => r.Type == RelationshipType.VALUE);
            var valueText = string.Empty;
            if (valueRelationship is not null)
            {
                var valueBlock = valueRelationship.Ids.Select(id => blockById.GetValueOrDefault(id)).FirstOrDefault(b => b is not null);
                if (valueBlock is not null)
                {
                    valueText = GetChildText(valueBlock, blockById);
                }
            }

            keyText = Normalize(keyText);
            valueText = Normalize(valueText);
            if (string.IsNullOrWhiteSpace(keyText) || string.IsNullOrWhiteSpace(valueText))
            {
                continue;
            }

            if (!dict.ContainsKey(keyText))
            {
                dict[keyText] = new KeyValueModel
                {
                    Key = keyText,
                    Value = valueText,
                    Page = keyBlock.Page ?? 1,
                    Confidence = keyBlock.Confidence ?? 0
                };
            }
        }

        return dict;
    }

    private static List<TableModel> ExtractTables(List<Block> blocks, Dictionary<string, Block> blockById)
    {
        var tables = new List<TableModel>();
        foreach (var tableBlock in blocks.Where(b => b.BlockType == BlockType.TABLE))
        {
            var table = new TableModel { Page = tableBlock.Page ?? 1 };
            var cellBlocks = GetChildrenByType(tableBlock, blockById, BlockType.CELL)
                .OrderBy(c => c.RowIndex)
                .ThenBy(c => c.ColumnIndex)
                .ToList();

            foreach (var grp in cellBlocks.GroupBy(c => c.RowIndex ?? 0))
            {
                var row = new TableRowModel { RowIndex = grp.Key };
                foreach (var cell in grp)
                {
                    row.Cells[cell.ColumnIndex ?? 0] = Normalize(GetChildText(cell, blockById));
                }
                table.Rows.Add(row);
            }

            if (table.Rows.Count > 0)
            {
                tables.Add(table);
            }
        }
        return tables;
    }

    private static IEnumerable<Block> GetChildrenByType(Block block, Dictionary<string, Block> blockById, BlockType type)
    {
        var childIds = block.Relationships?.Where(r => r.Type == RelationshipType.CHILD).SelectMany(r => r.Ids) ?? [];
        return childIds.Select(id => blockById.GetValueOrDefault(id)).Where(b => b is not null && b.BlockType == type)!;
    }

    private static string GetChildText(Block block, Dictionary<string, Block> blockById)
    {
        var childIds = block.Relationships?.Where(r => r.Type == RelationshipType.CHILD).SelectMany(r => r.Ids) ?? [];
        var words = childIds
            .Select(id => blockById.GetValueOrDefault(id))
            .Where(b => b is not null && (b.BlockType == BlockType.WORD || b.BlockType == BlockType.SELECTION_ELEMENT))
            .Select(b => b!.Text)
            .Where(t => !string.IsNullOrWhiteSpace(t));
        return string.Join(' ', words);
    }

    private static string Normalize(string? input) => string.Join(' ', (input ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim();
}
