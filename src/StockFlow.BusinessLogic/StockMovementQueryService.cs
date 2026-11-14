using StockFlow.BusinessLogic.Products;
using StockFlow.DataAccess;
using StockFlow.Domain;

namespace StockFlow.BusinessLogic;

public sealed class StockMovementQueryService(IStockMovementReadRepository repository)
{
    public StockMovementList Search(StockMovementFilter filter)
    {
        if (filter.DateFrom?.Date > filter.DateTo?.Date)
            throw new ValidationException("Дата початку не може бути пізнішою за кінцеву дату.");
        if (filter.Type is { } type && !Enum.IsDefined(type))
            throw new ValidationException("Оберіть тип накладної.");
        if (filter.WarehouseId is <= 0)
            throw new ValidationException("Оберіть склад.");
        return repository.Search(filter with
        {
            Number = filter.Number?.Trim(), DateFrom = filter.DateFrom?.Date, DateTo = filter.DateTo?.Date
        });
    }

    public StockMovementDetails GetById(int id) => repository.GetById(id)
        ?? throw new ValidationException("Накладну не знайдено. Оновіть список.");
}
