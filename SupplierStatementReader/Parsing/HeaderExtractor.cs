using System.Text.RegularExpressions;
using SupplierStatementReader.Textract;

namespace SupplierStatementReader.Parsing;

public sealed class HeaderExtractor
{
    private static readonly string[] SupplierLabels = ["supplier", "company", "from", "statement from"];
    private static readonly string[] CustomerLabels = ["customer", "account name", "bill to", "sold to", "client"];
    private static readonly string[] StatementDateLabels = ["statement date", "statement as of", "date"];

    public (string SupplierName, string CustomerName, string StatementDate) Extract(TextractDocumentModel doc)
    {
        var supplier = FindField(doc, SupplierLabels, RejectNameNoise);
        var customer = FindField(doc, CustomerLabels, RejectNameNoise);
        var statementDate = FindDateField(doc, StatementDateLabels);

        return (supplier, customer, statementDate);
    }

    private static string FindField(TextractDocumentModel doc, string[] labels, Func<string, bool> reject)
    {
        foreach (var kv in doc.KeyValues.Values.OrderBy(k => k.Page))
        {
            if (labels.Any(label => kv.Key.Contains(label, StringComparison.OrdinalIgnoreCase)) && !reject(kv.Value))
            {
                return kv.Value;
            }
        }

        foreach (var line in doc.Lines.Where(l => l.Page == 1).Take(30))
        {
            foreach (var label in labels)
            {
                var idx = line.Text.IndexOf(label, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    var candidate = line.Text[(idx + label.Length)..].Trim(':', '-', ' ');
                    if (!string.IsNullOrWhiteSpace(candidate) && !reject(candidate))
                    {
                        return candidate;
                    }
                }
            }
        }

        return string.Empty;
    }

    private static string FindDateField(TextractDocumentModel doc, string[] labels)
    {
        foreach (var kv in doc.KeyValues.Values.OrderBy(k => k.Page))
        {
            if (labels.Any(label => kv.Key.Contains(label, StringComparison.OrdinalIgnoreCase)))
            {
                var parsed = SanitizationService.NormalizeDate(kv.Value);
                if (!string.IsNullOrWhiteSpace(parsed))
                {
                    return parsed;
                }
            }
        }

        foreach (var line in doc.Lines.Where(l => l.Page == 1).Take(40))
        {
            if (!labels.Any(label => line.Text.Contains(label, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var parsed = SanitizationService.NormalizeDate(line.Text);
            if (!string.IsNullOrWhiteSpace(parsed))
            {
                return parsed;
            }
        }

        return string.Empty;
    }

    private static bool RejectNameNoise(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        var lower = value.ToLowerInvariant();
        if (lower.Contains("total") || lower.Contains("balance") || lower.Contains("invoice")) return true;
        if (Regex.IsMatch(value, @"^\d+$")) return true;
        return false;
    }
}
