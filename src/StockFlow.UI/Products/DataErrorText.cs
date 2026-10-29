namespace StockFlow.UI.Products;

public static class DataErrorText
{
    public const string ConnectionFailed =
        "Не вдалося отримати дані. Перевірте підключення до бази даних і спробуйте ще раз.";

    public static string From(Exception error) =>
        error is InvalidOperationException ? error.Message : ConnectionFailed;
}
