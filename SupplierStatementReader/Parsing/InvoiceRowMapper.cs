using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SupplierStatementReader.Models;

namespace SupplierStatementReader.Parsing;

public sealed class InvoiceRowMapper
{
    private readonly ILogger<InvoiceRowMapper> _logger;
    private static readonly string[] SummaryWords = ["total", "subtotal", "balance", "payment", "brought forward", "carried forward", "opening balance", "closing balance"];

    public InvoiceRowMapper(ILogger<InvoiceRowMapper> logger) => _logger = logger;

    public List<InvoiceItem> MapRows(List<InvoiceTableCandidate> candidates)
    {
        var results = new List<InvoiceItem>();

        foreach (var candidate in candidates)
        {
            foreach (var row in candidate.Table.Rows.Skip(1))
            {
                var invoiceNo = GetCell(row, candidate.InvoiceNoCol);
                var invoiceDate = SanitizationService.NormalizeDate(GetCell(row, candidate.InvoiceDateCol));
                var dueDate = SanitizationService.NormalizeDate(GetCell(row, candidate.DueDateCol));

                if (!LooksLikeInvoiceNo(invoiceNo))
                {
                    _logger.LogWarning("Skipping row {RowIndex}: missing/ambiguous invoice number.", row.RowIndex);
                    continue;
                }

                if (IsSummaryRow(row.Cells.Values))
                {
                    _logger.LogWarning("Skipping summary row {RowIndex}.", row.RowIndex);
                    continue;
                }

                var debit = SanitizationService.NormalizeAmount(GetCell(row, candidate.DebitCol));
                var credit = SanitizationService.NormalizeAmount(GetCell(row, candidate.CreditCol));

                if (candidate.DebitCol == 0 && candidate.CreditCol == 0)
                {
                    _logger.LogWarning("Skipping row {RowIndex}: amount columns ambiguous.", row.RowIndex);
                    debit = 0;
                    credit = 0;
                }

                results.Add(new InvoiceItem
                {
                    InvoiceNo = invoiceNo,
                    InvoiceDate = invoiceDate,
                    DueDate = dueDate,
                    DebitAmount = debit,
                    CreditAmount = credit
                });
            }
        }

        return results
            .GroupBy(i => $"{i.InvoiceNo}|{i.InvoiceDate}|{i.DebitAmount}|{i.CreditAmount}")
            .Select(g => g.First())
            .ToList();
    }

    private static string GetCell(Textract.TableRowModel row, int col) => col > 0 && row.Cells.TryGetValue(col, out var value) ? value : string.Empty;

    private static bool LooksLikeInvoiceNo(string input)
    {
        var text = input.Trim();
        return !string.IsNullOrWhiteSpace(text) && Regex.IsMatch(text, @"^[A-Za-z0-9\-/]{3,}$");
    }

    private static bool IsSummaryRow(IEnumerable<string> values)
    {
        var joined = string.Join(' ', values).ToLowerInvariant();
        return SummaryWords.Any(w => joined.Contains(w));
    }
}
