# SupplierStatementReader_AWS

Production-ready .NET 8 console app to parse supplier statement PDFs with AWS Textract and output conservative, confidence-first JSON.

## Setup

1. Configure `SupplierStatementReader/appsettings.json`:
   - `Aws.Region`
   - `Textract.StagingBucket` (required for PDF processing)
   - `Textract.StagingPrefix` (optional)
   - `Output.Folder` (optional)

2. Run:

```bash
dotnet restore
dotnet build
dotnet run --project SupplierStatementReader "C:\Statements"
dotnet run --project SupplierStatementReader "C:\Statements\sample.pdf"
```

## Output schema

```json
{
  "supplier_name": "",
  "customer_name": "",
  "statement_date": "",
  "invoicelist": [
    {
      "invoice_no": "",
      "invoice_date": "",
      "duedate": "",
      "debitamount": 0,
      "creditamount": 0
    }
  ]
}
```

## Notes
- Input path is always read from command-line args (never fixed in appsettings).
- If output folder is blank, JSON is written beside the source PDF.
- Parser defaults missing/ambiguous text to `""`, numbers to `0`, and invoice list to `[]`.
