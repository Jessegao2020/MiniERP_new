# PI and commercial invoice core decoupling

Both invoice types share Invoice / InvoiceItem. The desktop keeps its existing
editor, position-selection behavior and PDF rendering, but uses Application
InvoiceRules, InvoicePolicy and IInvoiceWorkflow for business operations.
No database migrations or MCP write tools are introduced.

- InvoiceRules: defaults, timestamp numbering, line snapshots, pricing,
  validation, typed preparation. SalesLineAmounts in Domain is shared with
  Quotation; catalog pricing/line validation reuse existing quotation helpers.
- InvoiceWorkflow: DTO create/update, source conversion, lookup, history,
  delete, and trusted desktop prepared-save adapter. Update replaces all
  line items; existing item IDs must belong to the document. Explicit prices
  are in document currency. Existing lines retain price snapshots if price is
  omitted. Currency changes with items require explicit target-currency prices in DTOs, or the shared confirmed desktop conversion plan.
- SalesDocumentCopyService continues producing editable, unsaved conversion
  drafts for desktop. Workflow ConvertAsync saves the converted document
  immediately when explicitly called by a trusted host. It copies source
  snapshots, not current catalog prices. No remote endpoint is exposed.
- InvoicePolicy provides separate PI/commercial prefixes, currency, delivery
  and payment defaults. The desktop loads optional InvoicePolicy in
  settings.json; top-level CnyPerUsd remains authoritative. Existing defaults
  are PI, INV, USD, FCA Baoding, 100% prepayment. No settings screen added.
- Invoice numbering keeps the existing millisecond timestamp format and
  manual number support; the unique index is still the final guard. No
  distributed allocation or retry yet. Document date and numbering timestamp
  are separate for DTO creation.
- Existing persisted type and provenance cannot be edited in place. New
  converted documents validate source existence/type. Historic prices and
  zero unused CNY rates are preserved. Due-date ordering is not tightened
  because advance-payment due dates can precede invoice dates.
- Repository returns new line IDs after update; desktop keeps them on
  repeated save. Existing downstream deletion guard remains.
- Entity adapter methods are not remote contracts. Authorization, tenant
  isolation, concurrency, idempotency and PDF extraction remain separate tasks.

Run from repository root:

    dotnet run --project tests/MiniERP.QuotationSmoke -c Release
    dotnet run --project tests/MiniERP.QuotationIntegration -c Release
    dotnet run --project tests/MiniERP.InvoiceIntegration -c Release
    dotnet build Desktop/MiniERP.Desktop.Avalonia -c Release
    dotnet build Infrastructure/MiniERP.McpServer -c Release

Integration creates two disposable SQLite files via EnsureCreated. The existing
catalog migration smoke suite tests migration compatibility separately.
Check desktop manual PI/CI creation, position editor, repeat save, currency
change, PDF, quotation/contract to invoice conversion, PI to commercial
invoice, invoice to packing list/contract and downstream deletion protection
with test data. The shared CI matrix runs quotation and invoice regressions.
