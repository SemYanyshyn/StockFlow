using StockFlow.Domain;

namespace StockFlow.DataAccess.Products;

public static class UnitOfMeasureColumn
{
    public static UnitOfMeasure Parse(string value) => value switch
    {
        "piece" => UnitOfMeasure.Piece,
        "kg" => UnitOfMeasure.Kilogram,
        "liter" => UnitOfMeasure.Liter,
        _ => throw new InvalidOperationException($"Невідома одиниця виміру в базі даних: '{value}'.")
    };
}
