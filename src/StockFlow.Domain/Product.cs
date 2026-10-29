namespace StockFlow.Domain;

public class Product
{
    public const int MaxNameLength = 100;

    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public UnitOfMeasure Unit { get; set; }
    public int CategoryId { get; set; }

    public bool Validate() =>
        !string.IsNullOrWhiteSpace(Name) &&
        Name.Trim().Length <= MaxNameLength &&
        CategoryId > 0 &&
        Enum.IsDefined(Unit);

    public bool MatchesSearch(string? query) =>
        string.IsNullOrWhiteSpace(query) ||
        Name.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase);
}
