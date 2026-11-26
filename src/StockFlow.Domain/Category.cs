namespace StockFlow.Domain;

public class Category
{
    public const int MaxNameLength = 100;

    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public bool Validate() =>
        !string.IsNullOrWhiteSpace(Name) && Name.Trim().Length <= MaxNameLength;

    public bool MatchesSearch(string? query) =>
        string.IsNullOrWhiteSpace(query) ||
        Name.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase);
}
