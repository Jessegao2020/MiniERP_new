# Contract core decoupling

The Contract module follows the quotation and invoice workflow pattern.
ContractRules owns draft defaults, numbering, catalog pricing, line validation,
typed document preparation and snapshot cloning. ContractWorkflow provides DI
create, update, convert, detail, history and delete methods. DTOs carry typed
commercial data, including lead time, bank information, customer PO, remarks
and terms and conditions. SavePreparedAsync is trusted desktop input only;
remote callers must use DTOs and require authorization before access.

Quotation and invoice conversion retains source identity and historical
snapshots; the desktop copy service continues to produce unsaved editable
drafts, and the workflow ConvertAsync explicitly persists a converted document.
Original contract contact snapshot behavior is preserved for converted documents.
Existing source provenance cannot be edited in place. Update replaces the item
collection, and omitted existing unit prices retain historical values if currency
and rate are unchanged. Omitted nullable header fields can clear their values.
New line IDs are returned to the editor after save.

ContractPolicy supports an optional settings.json object with NumberPrefix
(default CON), DefaultCurrency (USD), DeliveryTerm (FCA Baoding), PaymentTerm
(100% prepayment), LeadTime (2 business days). CnyPerUsd is still taken from the
existing top-level settings. No database migrations or new settings screen.
The timestamp numbering format and manual numbering are retained.

Contract uses the same CurrencyConversionService and CurrencyConversionDialog
as quotation and invoices, including all three price modes, cancellation,
historical exchange rates and manual lines. A downstream reference blocks a
currency change in both the editor and repository. No issued/signed/paid
lifecycle inference; a signed contract should not be overwritten just because
it has no downstream records. Existing deletion protection is retained.

Run from repository root:

    dotnet run --project tests/MiniERP.ContractIntegration -c Release
    dotnet run --project tests/MiniERP.QuotationSmoke -c Release
    dotnet run --project tests/MiniERP.QuotationIntegration -c Release
    dotnet run --project tests/MiniERP.InvoiceIntegration -c Release
    dotnet run --project Desktop/MiniERP.Desktop.Avalonia

The integration suite uses disposable real SQLite files initialized with
EnsureCreated, not production databases. It checks DTO validation, repeated
save, commercial clauses, conversion snapshots, downstream guard and the
existing packing-list draft copy path. The shared CI matrix runs all suites.
PDF and editable XLSX output still require manual visual regression.
No PDF/XLSX rendering changes, MCP writes, authentication, schema migration,
distributed numbering or cross-device concurrency are included.
