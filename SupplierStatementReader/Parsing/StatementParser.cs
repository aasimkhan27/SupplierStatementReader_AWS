using Microsoft.Extensions.Logging;
using SupplierStatementReader.Models;
using SupplierStatementReader.Textract;

namespace SupplierStatementReader.Parsing;

public sealed class StatementParser
{
    private readonly HeaderExtractor _headerExtractor;
    private readonly InvoiceTableDetector _tableDetector;
    private readonly InvoiceRowMapper _rowMapper;
    private readonly SanitizationService _sanitization;
    private readonly ILogger<StatementParser> _logger;

    public StatementParser(
        HeaderExtractor headerExtractor,
        InvoiceTableDetector tableDetector,
        InvoiceRowMapper rowMapper,
        SanitizationService sanitization,
        ILogger<StatementParser> logger)
    {
        _headerExtractor = headerExtractor;
        _tableDetector = tableDetector;
        _rowMapper = rowMapper;
        _sanitization = sanitization;
        _logger = logger;
    }

    public StatementResult Parse(TextractDocumentModel document)
    {
        _logger.LogInformation("Parser started");
        var header = _headerExtractor.Extract(document);
        var candidateTables = _tableDetector.Detect(document);
        var rows = _rowMapper.MapRows(candidateTables);

        var result = new StatementResult
        {
            SupplierName = header.SupplierName,
            CustomerName = header.CustomerName,
            StatementDate = header.StatementDate,
            InvoiceList = rows
        };

        var sanitized = _sanitization.Sanitize(result);

        _logger.LogInformation("Detected fields supplier='{Supplier}', customer='{Customer}', statement_date='{Date}', rows={Count}",
            sanitized.SupplierName, sanitized.CustomerName, sanitized.StatementDate, sanitized.InvoiceList.Count);
        _logger.LogInformation("Parser completed");

        return sanitized;
    }
}
