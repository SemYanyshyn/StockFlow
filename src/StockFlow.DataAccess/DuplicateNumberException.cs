namespace StockFlow.DataAccess;

public sealed class DuplicateNumberException : Exception
{
    public DuplicateNumberException() : base("Номер накладної вже існує")
    {
    }
}
