namespace StockFlow.Domain;

/// <summary>A read-only projection of a movement and its directory names.</summary>
public sealed record StockMovementDetails(
    int Id, string Number, DateTime MovementDate, MovementType Type,
    string ProductName, int Quantity, UnitOfMeasure Unit,
    string WarehouseName, string ResponsibleName, string? SupplierName, string? Note);

public sealed record StockMovementFilter(
    string? Number = null, MovementType? Type = null, int? WarehouseId = null,
    DateTime? DateFrom = null, DateTime? DateTo = null);

public sealed record StockMovementList(IReadOnlyList<StockMovementDetails> Items, bool HasAny);
