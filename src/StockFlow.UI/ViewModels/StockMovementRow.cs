using StockFlow.Domain;

namespace StockFlow.UI.ViewModels;

public sealed record StockMovementRow(StockMovementDetails Data)
{
    public int Id => Data.Id;
    public string Number => Data.Number;
    public DateTime Date => Data.MovementDate;
    public string TypeText => Data.Type == MovementType.Receipt ? "Надходження" : "Списання";
    public string ProductName => Data.ProductName;
    public string QuantityText => $"{Data.Quantity} {UnitOfMeasureText.Short(Data.Unit)}";
    public string WarehouseName => Data.WarehouseName;
    public string ResponsibleName => Data.ResponsibleName;
    public bool HasSupplier => Data.SupplierName is not null;
    public string NoteText => string.IsNullOrWhiteSpace(Data.Note) ? "Примітки немає" : Data.Note;
}

public sealed record MovementTypeOption(MovementType? Value, string Name);
public sealed record MovementWarehouseOption(int? Id, string Name);
