using Microsoft.Extensions.Logging.Abstractions;
using SupplierStatementReader.Parsing;
using SupplierStatementReader.Textract;

namespace SupplierStatementReader.Tests;

public sealed class StatementParserTests
{
    [Fact]
    public void Parse_MapsInvoiceRows_AndSkipsSummary()
    {
        var doc = new TextractDocumentModel
        {
            KeyValues = new Dictionary<string, KeyValueModel>
            {
                ["Supplier"] = new() { Key = "Supplier", Value = "ABC Supplies Ltd" },
                ["Customer"] = new() { Key = "Customer", Value = "XYZ Stores" },
                ["Statement Date"] = new() { Key = "Statement Date", Value = "15/02/2025" }
            },
            Tables =
            [
                new TableModel
                {
                    Rows =
                    [
                        new TableRowModel { RowIndex = 1, Cells = { [1] = "Invoice No", [2] = "Invoice Date", [3] = "Due Date", [4] = "Debit", [5] = "Credit" } },
                        new TableRowModel { RowIndex = 2, Cells = { [1] = "INV1001", [2] = "01/02/2025", [3] = "28/02/2025", [4] = "1,234.56", [5] = "" } },
                        new TableRowModel { RowIndex = 3, Cells = { [1] = "Total", [4] = "1,234.56" } }
                    ]
                }
            ]
        };

        var parser = new StatementParser(
            new HeaderExtractor(),
            new InvoiceTableDetector(),
            new InvoiceRowMapper(new NullLogger<InvoiceRowMapper>()),
            new SanitizationService(),
            new NullLogger<StatementParser>());

        var result = parser.Parse(doc);

        Assert.Equal("ABC Supplies Ltd", result.SupplierName);
        Assert.Equal("XYZ Stores", result.CustomerName);
        Assert.Equal("2025-02-15", result.StatementDate);
        Assert.Single(result.InvoiceList);
        Assert.Equal("INV1001", result.InvoiceList[0].InvoiceNo);
        Assert.Equal(1234.56m, result.InvoiceList[0].DebitAmount);
        Assert.Equal(0m, result.InvoiceList[0].CreditAmount);
    }
}
