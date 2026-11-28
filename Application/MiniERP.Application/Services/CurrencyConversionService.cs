using MiniERP.Domain;

namespace MiniERP.ApplicationLayer.Services;

public enum CurrencyConversionMode { ConvertExistingPrices, RepriceFromCatalog, KeepNumericPrices }

public sealed record CurrencyConversionLine(string Name, int? ArticleId, decimal Quantity,
    string Unit, decimal UnitPrice, decimal DiscountPercent, string Currency, decimal ExchangeRate);

public sealed record CurrencyConversionPreviewLine(string Name, decimal Quantity, decimal DiscountPercent,
    decimal OldUnitPrice, decimal NewUnitPrice, decimal OldTotal, decimal NewTotal,
    CurrencyConversionLine SourceLine);

public sealed record CurrencyConversionPlan(string SourceCurrency, string TargetCurrency, decimal Rate,
    CurrencyConversionMode Mode, IReadOnlyList<CurrencyConversionPreviewLine> Lines,
    decimal OldTotal, decimal NewTotal);

/// <summary>Pure all-or-nothing calculation. No persistence or UI dependencies.</summary>
public static class CurrencyConversionService
{
    public static decimal ResolveRate(decimal snapshot, decimal configuredRate)
        => snapshot > 0m ? snapshot : configuredRate;

    public static bool TryPlan(string source, string target, decimal rate, CurrencyConversionMode mode,
        IReadOnlyList<CurrencyConversionLine> lines, IReadOnlyCollection<Article> catalog,
        out CurrencyConversionPlan? plan, out string error)
    {
        plan = null;
        error = string.Empty;
        if (!QuotationRules.IsSupportedCurrency(source) || !QuotationRules.IsSupportedCurrency(target) || source == target)
        { error = "Select a different supported currency (USD or CNY)."; return false; }
        if (!Enum.IsDefined(mode))
        { error = "Unknown currency conversion mode."; return false; }
        if (lines.Count > 0 && rate <= 0m)
        { error = "Configure a positive CNY per USD exchange rate in Settings > System first."; return false; }

        var preview = new List<CurrencyConversionPreviewLine>(lines.Count);
        try
        {
            foreach (var line in lines)
            {
                if (!QuotationRules.TryValidatePosition(line.Name, line.Quantity, line.Unit,
                    line.UnitPrice, line.DiscountPercent, out error)) return false;
                if (line.Currency != source)
                { error = $"'{line.Name}' has a different currency from the document. Correct the line before conversion."; return false; }
                decimal price;
                switch (mode)
                {
                    case CurrencyConversionMode.ConvertExistingPrices:
                        price = target == "CNY" ? line.UnitPrice * rate : line.UnitPrice / rate;
                        price = decimal.Round(price, 2, MidpointRounding.AwayFromZero);
                        break;
                    case CurrencyConversionMode.RepriceFromCatalog:
                        var article = line.ArticleId is int id ? catalog.FirstOrDefault(a => a.Id == id) : null;
                        if (article?.Price is not decimal catalogPrice || catalogPrice < 0)
                        { error = $"'{line.Name}' has no valid linked catalog price. Use exchange-rate conversion or keep numeric prices instead."; return false; }
                        price = decimal.Round(target == "USD" ? catalogPrice / rate : catalogPrice,
                            2, MidpointRounding.AwayFromZero);
                        break;
                    default:
                        price = line.UnitPrice;
                        break;
                }
                preview.Add(new CurrencyConversionPreviewLine(line.Name, line.Quantity, line.DiscountPercent,
                    line.UnitPrice, price, SalesLineAmounts.Calculate(line.Quantity, line.UnitPrice, line.DiscountPercent),
                    SalesLineAmounts.Calculate(line.Quantity, price, line.DiscountPercent), line));
            }
            plan = new CurrencyConversionPlan(source, target, rate, mode, preview.AsReadOnly(),
                preview.Sum(l => l.OldTotal), preview.Sum(l => l.NewTotal));
            return true;
        }
        catch (OverflowException)
        {
            error = "The converted price or total exceeds the supported decimal range.";
            return false;
        }
    }

    public static CurrencyConversionLine From(QuotationItem line)
        => new(line.ArticleName, line.SourceArticleId, line.Quantity, line.Unit, line.UnitPrice,
            line.DiscountPercent, line.Currency, line.ExchangeRateSnapshot);
    public static CurrencyConversionLine From(ContractItem line)
        => new(line.ArticleName, line.SourceArticleId, line.Quantity, line.Unit, line.UnitPrice,
            line.DiscountPercent, line.Currency, line.ExchangeRateSnapshot);
    public static CurrencyConversionLine From(InvoiceItem line)
        => new(line.ArticleName, line.SourceArticleId, line.Quantity, line.Unit, line.UnitPrice,
            line.DiscountPercent, line.Currency, line.ExchangeRateSnapshot);
}

/// <summary>Dependency guard shared by the UI preview and persistence layer.</summary>
public interface IDocumentCurrencyGuard
{
    Task<bool> HasDownstreamAsync(DocumentSourceType sourceType, int id);
}
