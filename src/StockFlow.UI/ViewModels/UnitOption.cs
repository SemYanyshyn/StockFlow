using StockFlow.Domain;

namespace StockFlow.UI.ViewModels;

public sealed record UnitOption(UnitOfMeasure Unit, string Name)
{
    public static IReadOnlyList<UnitOption> All { get; } = Enum.GetValues<UnitOfMeasure>()
        .Select(unit => new UnitOption(unit, UnitOfMeasureText.Short(unit)))
        .ToList();

    public override string ToString() => Name;
}
