namespace SupplierStatementReader.Textract;

public sealed class TextractDocumentModel
{
    public List<PageTextLine> Lines { get; init; } = [];
    public List<TableModel> Tables { get; init; } = [];
    public Dictionary<string, KeyValueModel> KeyValues { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class PageTextLine
{
    public int Page { get; init; }
    public string Text { get; init; } = string.Empty;
}

public sealed class KeyValueModel
{
    public int Page { get; init; }
    public string Key { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public float Confidence { get; init; }
}

public sealed class TableModel
{
    public int Page { get; init; }
    public List<TableRowModel> Rows { get; init; } = [];
}

public sealed class TableRowModel
{
    public int RowIndex { get; init; }
    public Dictionary<int, string> Cells { get; init; } = new();
}
