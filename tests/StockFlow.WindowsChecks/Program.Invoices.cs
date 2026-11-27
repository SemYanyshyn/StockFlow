using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using StockFlow.BusinessLogic;
using StockFlow.BusinessLogic.Users;
using StockFlow.DataAccess;
using StockFlow.Domain;
using StockFlow.UI.ViewModels;
using StockFlow.UI.Views;

internal static partial class Program
{
    private static async Task CheckInvoices(ServiceProvider admin, ServiceProvider employee)
    {
        var adminList = admin.GetRequiredService<StockMovementsViewModel>();
        await adminList.LoadAsync();
        var adminView = new StockMovementsView { DataContext = adminList };
        await Render(adminView, "invoices-admin");
        Check(((Button)adminView.FindName("CreateMovementButton")).Visibility == Visibility.Collapsed &&
            !adminList.OpenCreateCommand.CanExecute(null), "invoice create button is hidden for Admin");

        var list = employee.GetRequiredService<StockMovementsViewModel>();
        var view = new StockMovementsView { DataContext = list };
        await list.LoadAsync();
        await Render(view, "invoices-employee");
        var employeeButton = (Button)view.FindName("CreateMovementButton");
        Check(employeeButton.Visibility == Visibility.Visible && employeeButton.IsEnabled,
            "invoice create button is visible and enabled for Employee");

        await list.OpenCreateCommand.ExecuteAsync(null);
        Check(list.IsCreateFormOpen, "invoice create form opens for Employee");
        var receiptNumber = list.CreationNumber;
        Check(receiptNumber.StartsWith("НК-", StringComparison.Ordinal),
            "invoice number is assigned automatically");

        var product = list.CreateProducts.Single(p => p.Name == "Молоко 2,5%");
        var warehouse = list.CreateWarehouses.Single(w => w.Name == "Основний склад");
        var stocks = employee.GetRequiredService<IStockRepository>();
        int Quantity() => stocks.GetAll(warehouse.Id).Single(s => s.ProductId == product.Id).Quantity;
        var before = Quantity();
        list.CreationDate = new DateTime(2026, 11, 13);
        list.SelectedCreateProduct = product;
        list.SelectedCreateWarehouse = warehouse;
        list.SelectedCreateSupplier = list.CreateSuppliers[0];
        list.SelectedCreateType = list.CreateTypes.Single(option => option.Value == MovementType.WriteOff);
        Check(!list.IsReceipt && list.SelectedCreateSupplier is null,
            "selecting WriteOff hides and clears the supplier");

        list.CreationQuantity = (before + 1).ToString(CultureInfo.InvariantCulture);
        await list.SaveMovementCommand.ExecuteAsync(null);
        Check(list.CreationError == "Недостатньо товару на складі" &&
            list.IsCreateFormOpen && Quantity() == before,
            "over-stock write-off displays the real service error without persisting");

        list.SelectedCreateType = list.CreateTypes.Single(option => option.Value == MovementType.Receipt);
        list.CreationQuantity = "0";
        await list.SaveMovementCommand.ExecuteAsync(null);
        Check(list.CreationError == "Кількість має бути додатним цілим числом",
            "invoice editor rejects zero quantity");

        list.CreationQuantity = "5";
        list.SelectedCreateSupplier = null;
        await list.SaveMovementCommand.ExecuteAsync(null);
        Check(list.CreationError == "Для надходження потрібно обрати постачальника",
            "receipt cannot be saved without a supplier");

        list.SelectedCreateSupplier = list.CreateSuppliers[0];
        list.CreationNote = "SE7-5 receipt";
        await list.SaveMovementCommand.ExecuteAsync(null);
        Check(!list.IsCreateFormOpen && list.IsListOpen &&
            list.Movements.Any(row => row.Number == receiptNumber &&
                row.Data.Type == MovementType.Receipt && row.HasSupplier) &&
            Quantity() == before + 5,
            "receipt commits, increases actual stock and refreshes the list");

        await list.OpenCreateCommand.ExecuteAsync(null);
        var writeOffNumber = list.CreationNumber;
        Check(writeOffNumber != receiptNumber,
            "a second form receives a different invoice number");
        list.CreationDate = new DateTime(2026, 11, 13);
        list.SelectedCreateProduct = product;
        list.SelectedCreateWarehouse = warehouse;
        list.SelectedCreateType = list.CreateTypes.Single(option => option.Value == MovementType.WriteOff);
        list.CreationQuantity = "2";
        await list.SaveMovementCommand.ExecuteAsync(null);
        Check(!list.IsCreateFormOpen &&
            list.Movements.Any(row => row.Number == writeOffNumber &&
                row.Data.Type == MovementType.WriteOff && !row.HasSupplier) &&
            Quantity() == before + 3,
            "write-off commits without supplier and refreshes the list");

        var day = new DateTime(2026, 11, 13);
        list.DateFrom = list.DateTo = day;
        list.FilterType = MovementType.Receipt;
        list.FilterWarehouseId = warehouse.Id;
        await CheckAutomaticFilter(() => list.Movements.Any(row => row.Number == receiptNumber) &&
            list.Movements.All(row => row.Number != writeOffNumber && row.Date.Date == day &&
                row.Data.Type == MovementType.Receipt && row.WarehouseName == warehouse.Name),
            "invoice filters apply automatically and combine date, type and warehouse");

        await list.ClearFiltersCommand.ExecuteAsync(null);
        list.SearchNumber = writeOffNumber.ToLowerInvariant();
        await CheckAutomaticFilter(() => list.Movements.Count == 1 && list.Movements[0].Number == writeOffNumber,
            "invoice number search is automatic and case-insensitive");

        list.SearchNumber = "SE7-5-absent-" + Guid.NewGuid().ToString("N");
        await CheckAutomaticFilter(() => list.Movements.Count == 0 &&
            list.EmptyMessage == "За обраними фільтрами накладних не знайдено." &&
            list.ListError is null,
            "empty search shows its own message, not an error");

        async Task CheckAutomaticFilter(Func<bool> matches, string description)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            try
            {
                while (list.IsBusy || !matches()) await Task.Delay(20, timeout.Token);
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException("The invoice list did not update: " + description);
            }
            Check(true, description);
        }
    }
}
