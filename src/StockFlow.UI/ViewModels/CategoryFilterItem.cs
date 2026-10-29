namespace StockFlow.UI.ViewModels;

public sealed record CategoryFilterItem(int? Id, string Name)
{
    public static CategoryFilterItem All { get; } = new(null, "Усі категорії");

    public override string ToString() => Name;
}
