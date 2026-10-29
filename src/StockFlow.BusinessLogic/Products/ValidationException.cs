namespace StockFlow.BusinessLogic.Products;

public sealed class ValidationException : Exception
{
    public ValidationException(string message) : base(message)
    {
    }
}
