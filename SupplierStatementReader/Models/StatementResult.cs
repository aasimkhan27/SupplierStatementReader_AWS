using System.Text.Json.Serialization;

namespace SupplierStatementReader.Models;

public sealed class StatementResult
{
    [JsonPropertyName("supplier_name")]
    public string SupplierName { get; set; } = string.Empty;

    [JsonPropertyName("customer_name")]
    public string CustomerName { get; set; } = string.Empty;

    [JsonPropertyName("statement_date")]
    public string StatementDate { get; set; } = string.Empty;

    [JsonPropertyName("invoicelist")]
    public List<InvoiceItem> InvoiceList { get; set; } = [];
}
