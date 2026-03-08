using SupplierStatementReader.Textract;

namespace SupplierStatementReader.Parsing;

public sealed class InvoiceTableDetector
{
    private static readonly string[] InvoiceCols = ["invoice no", "invoice number", "inv no", "reference", "ref", "document no", "doc no"];
    private static readonly string[] InvoiceDateCols = ["invoice date", "date"];
    private static readonly string[] DueCols = ["due date", "due"];
    private static readonly string[] DebitCols = ["debit", "charge", "amount"];
    private static readonly string[] CreditCols = ["credit", "payment"];

    public List<InvoiceTableCandidate> Detect(TextractDocumentModel document)
    {
        var candidates = new List<InvoiceTableCandidate>();
        foreach (var table in document.Tables)
        {
            var header = table.Rows.FirstOrDefault();
            if (header is null) continue;

            var map = new InvoiceTableCandidate { Table = table };
            foreach (var cell in header.Cells)
            {
                var norm = cell.Value.ToLowerInvariant();
                if (InvoiceCols.Any(c => norm.Contains(c))) map.InvoiceNoCol = cell.Key;
                else if (InvoiceDateCols.Any(c => norm.Contains(c))) map.InvoiceDateCol = cell.Key;
                else if (DueCols.Any(c => norm.Contains(c))) map.DueDateCol = cell.Key;
                else if (DebitCols.Any(c => norm.Contains(c))) map.DebitCol = cell.Key;
                else if (CreditCols.Any(c => norm.Contains(c))) map.CreditCol = cell.Key;
            }

            map.Score = (map.InvoiceNoCol > 0 ? 2 : 0)
                + (map.InvoiceDateCol > 0 ? 1 : 0)
                + (map.DueDateCol > 0 ? 1 : 0)
                + (map.DebitCol > 0 ? 1 : 0)
                + (map.CreditCol > 0 ? 1 : 0);

            if (map.Score >= 3 && map.InvoiceNoCol > 0)
            {
                candidates.Add(map);
            }
        }

        return candidates.OrderByDescending(c => c.Score).ToList();
    }
}

public sealed class InvoiceTableCandidate
{
    public TableModel Table { get; init; } = new();
    public int InvoiceNoCol { get; set; }
    public int InvoiceDateCol { get; set; }
    public int DueDateCol { get; set; }
    public int DebitCol { get; set; }
    public int CreditCol { get; set; }
    public int Score { get; set; }
}
