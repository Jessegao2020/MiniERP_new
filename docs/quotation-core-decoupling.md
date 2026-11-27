# Quotation core decoupling

Quotation business rules live in Application/Domain. Avalonia owns selection,
input parsing, notifications, dialogs and rendering. The current .NET 8
runtime, SQLite schema and PDF output remain unchanged.

## Use cases

- QuotationRules: draft defaults, currency conversion, explicit repricing,
  validation, document snapshots, and preparing typed input.
- QuotationNumberAllocator: existing date/code/sequence convention; a process-local gate.
- QuotationCopy: independent copy of a historical quotation snapshot.
- IQuotationWorkflow / QuotationWorkflow: DI-scoped create, update, copy, delete,
  detail, history and number preview. Desktop create/update/copy/delete now route
  through this workflow.
- QuotationDtos: request and response contracts for future MCP/API adapters.
  Create/Update resolve customer, salesperson, contact and new catalog-item IDs
  from the database. Explicit prices use the document currency; new catalog
  lines without an explicit price use the configured exchange rate. Existing
  line IDs must belong to the quotation; omitted existing unit prices preserve
  their historical values, unless the currency/rate changes (explicit price
  required). Update replaces the line collection, null validity clears it,
  and omitted contact preserves the old snapshot for the same customer.
  Update is not a PATCH; supply the intended commercial terms and discounts.
- SavePreparedAsync and DuplicatePreparedAsync are trusted in-process desktop
  adapters for the existing edit buffer. Remote callers must use the DTO methods,
  not bind raw domain entities. IQuotationService remains the low-level CRUD
  service for reads, persistence and legacy callers; it is not an API boundary.
  No new MCP write tool or HTTP endpoint has been exposed.

## Validation and compatibility

New writes and changed monetary lines enforce currency/rate consistency,
valid customer/user identities, contact membership, date order, line identity
ownership, quantities, prices and discounts. Unchanged legacy date/monetary
inconsistencies can still be saved through the prepared desktop path; read-only
queries do not rewrite history. Existing document numbers and historical
prices remain intact. Copies are new documents and must pass current validation.
Header-only save remains supported; PDF export requires at least one line.
Downstream document deletion protection remains at repository level.
Newly inserted line IDs are returned to the desktop after saving so repeated
updates retain their identities.

## Company quotation policy

QuotationPolicy carries number code, default currency, validity, delivery,
payment, lead time and configured CNY per USD rate. Hosts may register an
alternative policy with DI. The desktop reads the optional QuotationPolicy
object in its existing settings.json; the current top-level DefaultValidityDays
and CnyPerUsd settings remain authoritative for those two values. For example:

    {
      "CnyPerUsd": 7.0,
      "DefaultValidityDays": 30,
      "ThemeMode": "System",
      "QuotationPolicy": {
        "NumberCode": "INTPAN",
        "DefaultCurrency": "USD",
        "DeliveryTerm": "FCA Baoding",
        "PaymentTerm": "100% prepayment",
        "LeadTime": "2 business days"
      }
    }

Old settings files retain their defaults. Setting changes preserve the optional
policy object. No new UI configuration screen or database migration is required.
Only USD/CNY are supported at this stage. A new numbering code affects future
automatic numbers only; existing numbers are not rewritten.

## Verification

From the repository root:

    dotnet run --project tests/MiniERP.QuotationSmoke -c Release
    dotnet run --project tests/MiniERP.QuotationIntegration -c Release
    dotnet build Desktop/MiniERP.Desktop.Avalonia -c Release
    dotnet build Infrastructure/MiniERP.McpServer -c Release
    python3 Infrastructure/MiniERP.McpServer/smoke.py

The first suite checks pure business rules and numbering preview with a fake
service. The second uses the real DI workflow, EF Core repositories and two
disposable on-disk SQLite databases. It exercises creation, history, edits,
repeated save, snapshot retention, copies, invalid requests and deletion.
It creates the current schema with EnsureCreated; the existing CatalogSmoke
suite separately covers migration and split-database upgrade safety. Neither
suite uses the operator's real database.

Use a disposable database for desktop regression: create, edit, select
catalog items, manual lines, switch currency, save again, duplicate from both
list and editor, export PDF and test MCP reads. The MCP smoke script requires
initialized databases and remains read-only.

## Deferred

Cross-process numbering transactions/retries, authorization and tenant isolation,
offline sync, PostgreSQL, PDF extraction, other sales documents and MCP write
tools are not part of this change. The database unique index still protects
against duplicate numbers, but the allocator gate is process-local. The
salesperson ID in the DTO is a commercial document field, not an authenticated
actor; future adapters must authorize it and all resource access.
