using System.Text.Json.Serialization;

namespace SupplierStatementReader.Models;

public sealed class InvoiceItem
{
    [JsonPropertyName("invoice_no")]
    public string InvoiceNo { get; set; } = string.Empty;

    [JsonPropertyName("invoice_date")]
    public string InvoiceDate { get; set; } = string.Empty;

    [JsonPropertyName("duedate")]
    public string DueDate { get; set; } = string.Empty;

    [JsonPropertyName("debitamount")]
    public decimal DebitAmount { get; set; }

    [JsonPropertyName("creditamount")]
    public decimal CreditAmount { get; set; }
}
