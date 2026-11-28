# Shared document currency conversion

Quotation, PI and Commercial Invoice use the same pure Application
CurrencyConversionService and the same Avalonia confirmation dialog. The service
returns an immutable plan of all line prices and totals; preview does not change
the document. The UI applies it only after confirmation and only if the source
lines are still unchanged. Save persists the document. Cancel keeps the original
currency and prices.

Three modes:
- Convert existing prices (default): USD to CNY multiply, CNY to USD divide.
  Existing quantities and percentage discounts are preserved.
- Reprice from catalog: fresh linked CNY catalog prices, converted to the target
  currency. A missing article or price blocks the entire mode.
- Keep numeric prices: preserves unit-price digits, changes the currency and
  commercial value; explicit acknowledgment required.

Converted/repriced unit prices use two decimals, midpoint away from zero; line
totals use the existing Domain SalesLineAmounts rule. A round trip can incur
rounding differences. All modes with items require a positive rate; historical
document rate takes precedence over system settings. Empty documents can change
currency without a rate. Only USD/CNY are supported today. The UI has no
editable per-conversion rate field yet.

The shared downstream guard checks invoice, contract and packing-list references
before opening the preview, and repository update checks again before writing.
It applies even if the caller bypasses the UI. This does not implement a
distributed transaction across multiple devices. Existing lifecycle remains
unchanged: no issued/paid status is inferred from absence of downstream records.
For an already-issued document, use a new/revised document rather than overwrite.

The DTO workflows retain explicit target-currency prices for existing lines on
a currency change; callers can use the pure planner to obtain those prices.
No MCP write tools or new remote endpoints. Contract can reuse the planner and
dialog after its own core decoupling; it is not changed here.

Tests are integrated into the existing QuotationSmoke, QuotationIntegration
and InvoiceIntegration projects and their three-platform CI. For manual
regression, verify all three modes in all editors, cancel, invalid catalog
and FX, manual items, uncommitted position edits, save/reopen, no-item switches
and downstream protection. No schema migration or PDF template changes.
