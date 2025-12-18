using MiniERP.UI.Model;

namespace MiniERP.UI.Interface
{
    public interface ICountryProvider
    {
        IReadOnlyList<Country> GetAll();
        Country? FindByCode(string? code);
    }

    public class StaticCountryProvider : ICountryProvider
    {
        private static readonly Country[] _all =
        {
            new("CN", "China"),
            new("GB", "United Kingdom"),
            new("DE", "Germany"),
        };

        public Country? FindByCode(string? code)
            => string.IsNullOrWhiteSpace(code)
                ? null
                : _all.FirstOrDefault(x => x.Code.Equals(code.Trim(), StringComparison.OrdinalIgnoreCase));

        public IReadOnlyList<Country> GetAll() => _all;
    }
}
