using StockFlow.Domain;

namespace StockFlow.DataAccess;

public static class StockMovementTypeColumn
{
    public static string Format(MovementType type) => type switch
    {
        MovementType.Receipt => "receipt",
        MovementType.WriteOff => "write_off",
        _ => throw new ArgumentOutOfRangeException(nameof(type), "Невідомий тип руху товару")
    };

    public static MovementType Parse(string value) => value switch
    {
        "receipt" => MovementType.Receipt,
        "write_off" => MovementType.WriteOff,
        _ => throw new InvalidOperationException("Невідомий тип накладної.")
    };
}
