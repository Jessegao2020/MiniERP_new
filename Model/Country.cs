namespace MiniERP.UI.Model
{
    public sealed record Country(string Code, string Name)
    {
        public string Display => $"{Code}|{Name}";
    }
}
