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

        IStockTransaction? scope = null;
        try
        {
            scope = transactions.Begin();
            var stock = stocks.GetForUpdate(movement.ProductId, movement.WarehouseId, scope.Transaction);
            // Apply uses the domain's single validation source and mutates only after all checks pass.
            movement.Apply(stock);
            var id = movements.Add(movement, scope.Transaction);
            stocks.Save(stock, scope.Transaction);
            scope.Commit();
            return StockMovementRegistrationResult.Success(id);
        }
        catch (StockMovementValidationException error)
        {
            Rollback(scope);
            return StockMovementRegistrationResult.Failure(error.Message);
        }
        catch (DuplicateNumberException error)
        {
            Rollback(scope);
            return StockMovementRegistrationResult.Failure(error.Message);
        }
        catch (PostgresException error) when (
            error.SqlState is "40001" or "40P01" ||
            error.SqlState == "23505" && error.ConstraintName == "stock_pkey")
        {
            Rollback(scope);
            return StockMovementRegistrationResult.Failure("Спробуйте ще раз");
        }
        catch (PostgresException error) when (
            error.SqlState.StartsWith("08", StringComparison.Ordinal) ||
            error.SqlState is "57P01" or "57P02" or "57P03")
        {
            Rollback(scope);
            return StockMovementRegistrationResult.Failure("Не вдалося з'єднатися з базою даних. Перевірте підключення");
        }
        catch (PostgresException)
        {
            Rollback(scope);
            return StockMovementRegistrationResult.Failure("Не вдалося зберегти рух товару");
        }
        catch (Exception error) when (error is NpgsqlException or TimeoutException or IOException)
        {
            Rollback(scope);
            return StockMovementRegistrationResult.Failure("Не вдалося з'єднатися з базою даних. Перевірте підключення");
        }
        catch (Exception)
        {
            Rollback(scope);
            return StockMovementRegistrationResult.Failure("Не вдалося зберегти рух товару");
        }
        finally
        {
            // Cleanup errors cannot expose provider text or turn a committed success into a retry.
            try { scope?.Dispose(); }
            catch (Exception) { }
        }
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
