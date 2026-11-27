# Quotation core decoupling

The first extraction keeps .NET 8, existing SQLite schema, desktop layout and PDF
rendering unchanged. The Application project owns draft defaults, exchange-rate
snapshots, currency conversion, explicit repricing plans, line validation, header
snapshots, document preparation, copying and saving. The Domain owns the line
total formula. Avalonia owns text input, selection, property notifications,
dialogues, status messages and presenting/applying the results.

- QuotationRules: typed inputs, no Avalonia or database dependency.
- QuotationNumberAllocator: existing numbering convention and process-local gate.
- QuotationCopy: independent copy of a quotation snapshot.
- QuotationWorkflow: validated save, automatic or manual number, duplicate check.

The existing desktop number/copy helpers are compatibility adapters only.
Catalog prices are CNY untaxed; USD conversion uses CNY per USD. Historical
prices and exchange-rate snapshots remain unchanged except explicit user repricing.
Header-only save remains allowed; export preparation requires at least one line.

Run from repository root:

    dotnet run --project tests/MiniERP.QuotationSmoke -c Release
    dotnet build Desktop/MiniERP.Desktop.Avalonia -c Release
    dotnet build Infrastructure/MiniERP.McpServer -c Release
    python3 Infrastructure/MiniERP.McpServer/smoke.py

The quotation smoke uses an in-memory service double, not a production database.
Also verify desktop creation, explicit article selection, manual lines, currency
switching, save/update/duplicate, and PDF export using a disposable database.
The smoke script for MCP requires initialized test databases.

Not in scope: PDF extraction, database migration, remote API, authentication,
MCP write tools, runtime upgrade, or distributed numbering. The existing
database unique constraint protects against duplicate persisted numbers, but
the allocator gate does not coordinate multiple processes; future remote write
support needs transactional allocation and retry. This is an extraction of
quotation use cases, not authorization enforcement or a refactor of every
other sales document.
