namespace StockFlow.Domain;

public sealed class StockMovement
{
    public const int MaxNumberLength = 50;

    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public MovementType Type { get; set; }
    public int ProductId { get; set; }
    public int WarehouseId { get; set; }
    public int? SupplierId { get; set; }
    public int UserId { get; set; }
    public int Quantity { get; set; }
    public DateTime MovementDate { get; set; }
    public string? Note { get; set; }

    // The UML contract remains bool Validate(Stock) / void Apply(Stock).
    // The additional method shares the same rules and messages with callers.
    public bool Validate(Stock stock) => GetValidationError(stock) is null;

    public string? GetValidationError(Stock? stock)
    {
        if (string.IsNullOrWhiteSpace(Number))
            return "Номер накладної обов’язковий";
        if (Number.Length > MaxNumberLength)
            return "Номер накладної не може перевищувати 50 символів";
        if (Quantity <= 0)
            return "Кількість має бути більшою за 0";
        if (!Enum.IsDefined(Type))
            return "Невідомий тип руху товару";
        if (Id < 0 || ProductId <= 0 || WarehouseId <= 0 || UserId <= 0)
            return "Некоректні ідентифікатори руху товару";
        if (MovementDate == default)
            return "Потрібно вказати дату руху товару";
        if (Type == MovementType.Receipt && SupplierId is not > 0)
            return "Для надходження потрібно обрати постачальника";
        if (Type == MovementType.WriteOff && SupplierId is not null)
            return "Для списання постачальник не зазначається";
        if (stock is null)
            return "Не отримано залишок товару на складі";
        if (stock.ProductId <= 0 || stock.WarehouseId <= 0 || stock.Quantity < 0)
            return "Некоректний залишок товару на складі";
        if (stock.ProductId != ProductId || stock.WarehouseId != WarehouseId)
            return "Залишок не відповідає обраному товару та складу";
        if (Type == MovementType.WriteOff && Quantity > stock.Quantity)
            return "Недостатньо товару на складі";
        if (Type == MovementType.Receipt && Quantity > int.MaxValue - stock.Quantity)
            return "Кількість товару перевищує допустиме значення";

        return null;
    }

    public void Apply(Stock stock)
    {
        var error = GetValidationError(stock);
        if (error is not null)
            throw new StockMovementValidationException(error);

        // Compute before assignment so a rejected operation never partially changes Stock.
        var nextQuantity = Type == MovementType.Receipt
            ? checked(stock.Quantity + Quantity)
            : checked(stock.Quantity - Quantity);
        stock.Quantity = nextQuantity;
    }
}

public sealed class StockMovementValidationException : InvalidOperationException
{
    public StockMovementValidationException(string message) : base(message)
    {
    }
}
