using System.Data;
using System.IO;
using Npgsql;
using StockFlow.BusinessLogic.Users;
using StockFlow.DataAccess;
using StockFlow.Domain;

namespace StockFlow.BusinessLogic;

public sealed record StockMovementRegistrationResult(bool IsSuccess, int? MovementId, string? Error)
{
    public static StockMovementRegistrationResult Success(int id) => new(true, id, null);
    public static StockMovementRegistrationResult Failure(string error) => new(false, null, error);
}

/// <summary>Registers movements using the shared domain and team repository contract.</summary>
public sealed class StockMovementService(
    ICurrentUserContext currentUser,
    IStockRepository stocks,
    IStockMovementRepository movements,
    IStockTransactionFactory transactions)
{
    public StockMovementRegistrationResult Register(StockMovement request)
    {
        // Read the trusted user once. Never accept the caller's editable UserId.
        var user = currentUser.CurrentUser;
        if (user.Role != UserRole.Employee)
            return StockMovementRegistrationResult.Failure("Створювати рух товару може лише Працівник");
        if (user.Id <= 0)
            return StockMovementRegistrationResult.Failure("Не вдалося визначити поточного користувача");
        if (request is null)
            return StockMovementRegistrationResult.Failure("Не задано рух товару");

        var movement = new StockMovement
        {
            Number = request.Number,
            Type = request.Type,
            ProductId = request.ProductId,
            WarehouseId = request.WarehouseId,
            SupplierId = request.SupplierId,
            UserId = user.Id,
            Quantity = request.Quantity,
            MovementDate = request.MovementDate,
            Note = request.Note
        };

        return InTransaction(transaction =>
        {
            var stock = stocks.GetForUpdate(movement.ProductId, movement.WarehouseId, transaction);
            // Apply uses the domain's single validation source and mutates only after all checks pass.
            movement.Apply(stock);
            var id = movements.Add(movement, transaction);
            stocks.Save(stock, transaction);
            return id;
        });
    }

    private StockMovementRegistrationResult InTransaction(Func<IDbTransaction, int> persist)
    {
        IStockTransaction? scope = null;
        try
        {
            scope = transactions.Begin();
            var id = persist(scope.Transaction);
            scope.Commit();
            return StockMovementRegistrationResult.Success(id);
        }
        catch (Exception error)
        {
            Rollback(scope);
            return StockMovementRegistrationResult.Failure(SafeMessage(error));
        }
        finally
        {
            // Cleanup errors cannot expose provider text or turn a committed success into a retry.
            try { scope?.Dispose(); }
            catch (Exception) { }
        }
    }

    private static string SafeMessage(Exception error)
    {
        const string connectionError = "Не вдалося з'єднатися з базою даних. Перевірте підключення";
        const string saveError = "Не вдалося зберегти рух товару";
        return error switch
        {
            StockMovementValidationException validation => validation.Message,
            DuplicateNumberException => "Накладна з таким номером уже існує",
            PostgresException p when p.SqlState is "40001" or "40P01" ||
                p.SqlState == "23505" && p.ConstraintName == "stock_pkey" => "Спробуйте ще раз",
            PostgresException p when p.SqlState.StartsWith("08", StringComparison.Ordinal) ||
                p.SqlState is "57P01" or "57P02" or "57P03" => connectionError,
            PostgresException => saveError,
            NpgsqlException or TimeoutException or IOException => connectionError,
            _ => saveError
        };
    }

    private static void Rollback(IStockTransaction? scope)
    {
        try
        {
            if (scope?.IsActive == true) scope.Rollback();
        }
        catch (Exception)
        {
            // Dispose still releases the connection; preserve the original safe operation result.
        }
    }
}
