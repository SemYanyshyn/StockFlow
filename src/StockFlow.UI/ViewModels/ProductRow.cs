namespace StockFlow.UI.ViewModels;

public sealed record ProductRow(int Id, string Name, int CategoryId, string CategoryName, string Unit)
{
    public override string ToString() => Name;
}
