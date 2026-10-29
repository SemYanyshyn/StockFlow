namespace StockFlow.UI.Products;

public sealed record CategoryFilterItem(int? Id, string Name)
{
    public static CategoryFilterItem All { get; } = new(null, "Усі категорії");
}
