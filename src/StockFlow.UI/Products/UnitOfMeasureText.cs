using StockFlow.Domain;

namespace StockFlow.UI.Products;

public static class UnitOfMeasureText
{
    public static string Short(UnitOfMeasure unit) => unit switch
    {
        UnitOfMeasure.Piece => "шт.",
        UnitOfMeasure.Kilogram => "кг",
        UnitOfMeasure.Liter => "л",
        _ => unit.ToString()
    };
}
