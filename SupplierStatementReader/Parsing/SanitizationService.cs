using System.Globalization;
using System.Text.RegularExpressions;
using SupplierStatementReader.Models;

namespace SupplierStatementReader.Parsing;

public sealed class SanitizationService
{
    private static readonly string[] DateFormats = ["dd/MM/yyyy", "d/M/yyyy", "d/M/yy", "dd-MM-yyyy", "yyyy-MM-dd", "dd.MM.yyyy", "MM/dd/yyyy", "M/d/yyyy"];

    public StatementResult Sanitize(StatementResult input)
    {
        input.SupplierName = NormalizeName(input.SupplierName);
        input.CustomerName = NormalizeName(input.CustomerName);
        input.StatementDate = NormalizeDate(input.StatementDate);
        input.InvoiceList ??= [];

        foreach (var invoice in input.InvoiceList)
        {
            invoice.InvoiceNo = invoice.InvoiceNo?.Trim() ?? string.Empty;
            invoice.InvoiceDate = NormalizeDate(invoice.InvoiceDate);
            invoice.DueDate = NormalizeDate(invoice.DueDate);
            invoice.DebitAmount = invoice.DebitAmount;
            invoice.CreditAmount = invoice.CreditAmount;
        }

        return input;
    }

    public static string NormalizeDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var cleaned = value.Trim();

        foreach (var token in Regex.Split(cleaned, @"\s+|,").Where(t => !string.IsNullOrWhiteSpace(t)))
        {
            if (DateTime.TryParseExact(token, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                return dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
        }

        if (DateTime.TryParse(cleaned, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return string.Empty;
    }

    public static decimal NormalizeAmount(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0m;
        var cleaned = value.Trim().Replace("£", "").Replace("$", "").Replace(",", "").Replace(" ", "");
        if (cleaned.StartsWith('(') && cleaned.EndsWith(')'))
        {
            cleaned = $"-{cleaned[1..^1]}";
        }

        return decimal.TryParse(cleaned, NumberStyles.Number | NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount)
            ? amount
            : 0m;
    }

    private static string NormalizeName(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var lower = text.ToLowerInvariant();
        if (lower.Contains("total") || lower.Contains("balance") || Regex.IsMatch(text, @"^\d+$")) return string.Empty;
        return text;
    }
}
