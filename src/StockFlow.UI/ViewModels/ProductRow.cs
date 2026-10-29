namespace StockFlow.UI.ViewModels;

public sealed record ProductRow(string Name, string CategoryName, string Unit)
{
    public override string ToString() => Name;
}
