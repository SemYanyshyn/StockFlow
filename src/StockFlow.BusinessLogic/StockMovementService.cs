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

        return ExecuteInTransaction(scope =>
        {
            var stock = stocks.GetForUpdate(movement.ProductId, movement.WarehouseId, scope.Transaction);
            // Apply uses the domain's single validation source and mutates only after all checks pass.
            movement.Apply(stock);
            var id = movements.Add(movement, scope.Transaction);
            stocks.Save(stock, scope.Transaction);
            return id;
        });
    }

    private StockMovementRegistrationResult ExecuteInTransaction(Func<IStockTransaction, int> operation)
    {
        IStockTransaction? scope = null;
        try
        {
            scope = transactions.Begin();
            var id = operation(scope);
            scope.Commit();
            return StockMovementRegistrationResult.Success(id);
        }
        catch (StockMovementValidationException error)
        {
            return RollbackAndFail(scope, error.Message);
        }
        catch (DuplicateNumberException)
        {
            return RollbackAndFail(scope, "Накладна з таким номером уже існує");
        }
        catch (PostgresException error) when (
            error.SqlState is "40001" or "40P01" ||
            error.SqlState == "23505" && error.ConstraintName == "stock_pkey")
        {
            return RollbackAndFail(scope, "Спробуйте ще раз");
        }
        catch (PostgresException error) when (
            error.SqlState.StartsWith("08", StringComparison.Ordinal) ||
            error.SqlState is "57P01" or "57P02" or "57P03")
        {
            return RollbackAndFail(scope, "Не вдалося з'єднатися з базою даних. Перевірте підключення");
        }
        catch (PostgresException)
        {
            return RollbackAndFail(scope, "Не вдалося зберегти рух товару");
        }
        catch (Exception error) when (error is NpgsqlException or TimeoutException or IOException)
        {
            return RollbackAndFail(scope, "Не вдалося з'єднатися з базою даних. Перевірте підключення");
        }
        catch (Exception)
        {
            return RollbackAndFail(scope, "Не вдалося зберегти рух товару");
        }
        finally
        {
            // Cleanup errors cannot expose provider text or turn a committed success into a retry.
            try { scope?.Dispose(); }
            catch (Exception) { }
        }
    }

    private static StockMovementRegistrationResult RollbackAndFail(IStockTransaction? scope, string message)
    {
        Rollback(scope);
        return StockMovementRegistrationResult.Failure(message);
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
