using System.Globalization;
using MiniERP.ApplicationLayer.Services;
using MiniERP.Domain;

namespace MiniERP.Desktop.Infrastructure;

/// <summary>
/// YY + INTPAN + MMDD + User.Code (one or two digits) + daily sequence (01..99).
/// The sequence is scoped by calendar date and user code, not by a draft's open time.
/// </summary>
public static class QuotationNumberSupport
{
    private static readonly SemaphoreSlim CreateGate = new(1, 1);

    public static async Task<string> PreviewNextAsync(
        IQuotationService service, int userCode, DateTime date)
    {
        var prefix = GetPrefix(userCode, date);
        var numbers = await service.GetQuotationNumbersByPrefixAsync(prefix);
        var lastSequence = 0;

        foreach (var number in numbers)
        {
            // Require exactly two trailing digits. This prevents a one-digit
            // user code from accidentally consuming another user's two-digit code.
            if (number.Length != prefix.Length + 2 ||
                !number.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            var suffix = number.AsSpan(prefix.Length);
            if (!char.IsAsciiDigit(suffix[0]) || !char.IsAsciiDigit(suffix[1]))
                continue;

            var sequence = (suffix[0] - '0') * 10 + (suffix[1] - '0');
            lastSequence = Math.Max(lastSequence, sequence);
        }

        if (lastSequence >= 99)
            throw new InvalidOperationException(
                $"All 99 quotation numbers for {date:yyyy-MM-dd} and User Code {userCode} have been used.");

        return prefix + (lastSequence + 1).ToString("D2", CultureInfo.InvariantCulture);
    }

    public static async Task CreateAsync(
        IQuotationService service, Quotation quotation, int userCode)
    {
        // All new/copy flows in this desktop process use this shared gate,
        // so opening several drafts does not issue the same saved number.
        await CreateGate.WaitAsync();
        try
        {
            quotation.QuotationNumber = await PreviewNextAsync(service, userCode, DateTime.Now);
            await service.CreateQuotationAsync(quotation);
        }
        finally
        {
            CreateGate.Release();
        }
    }

    private static string GetPrefix(int userCode, DateTime date)
    {
        if (userCode is < 1 or > 99)
            throw new InvalidOperationException(
                "Set the selected user's Code to a one- or two-digit number (1-99) in Settings > User.");

        return date.ToString("yy", CultureInfo.InvariantCulture)
            + "INTPAN"
            + date.ToString("MMdd", CultureInfo.InvariantCulture)
            + userCode.ToString(CultureInfo.InvariantCulture);
    }
}
