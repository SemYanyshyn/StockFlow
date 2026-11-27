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
            !adminList.CreateCommand.CanExecute(null), "invoice create button is hidden for Admin");

        var list = employee.GetRequiredService<StockMovementsViewModel>();
        var view = new StockMovementsView { DataContext = list };
        await list.LoadAsync();
        await Render(view, "invoices-employee");
        var employeeButton = (Button)view.FindName("CreateMovementButton");
        Check(employeeButton.Visibility == Visibility.Visible && employeeButton.IsEnabled,
            "invoice create button is visible and enabled for Employee");

        await list.CreateCommand.ExecuteAsync(null);
        var editor = list.Editor ?? throw new InvalidOperationException(list.ListError ?? "Invoice editor was not opened.");
        await Render(view, "invoices-receipt-form", 950, 820);
        var editorView = Descendants<StockMovementEditorView>(view).Single();
        var supplierField = (StackPanel)editorView.FindName("SupplierField");
        var numberBox = (TextBox)editorView.FindName("NumberBox");
        var responsibleBox = (TextBox)editorView.FindName("ResponsibleBox");
        var currentUser = employee.GetRequiredService<ICurrentUserContext>().CurrentUser;
        var receiptNumber = editor.Number;
        Check(editor.ShowsSupplier && supplierField.Visibility == Visibility.Visible,
            "supplier field is visible for a receipt");
        Check(numberBox.IsReadOnly && numberBox.Text == receiptNumber && receiptNumber.StartsWith("НК-", StringComparison.Ordinal) &&
            responsibleBox.IsReadOnly && responsibleBox.Text == currentUser.FullName,
            "invoice number is assigned automatically and both number and responsible person are read-only");

        var product = editor.Products.Single(p => p.Name == "Молоко 2,5%");
        var warehouse = editor.Warehouses.Single(w => w.Name == "Основний склад");
        var stocks = employee.GetRequiredService<IStockRepository>();
        int Quantity() => stocks.GetAll(warehouse.Id).Single(s => s.ProductId == product.Id).Quantity;
        var before = Quantity();
        var day = new DateTime(2026, 11, 13);
        editor.Date = day;
        editor.SelectedProduct = product;
        editor.SelectedWarehouse = warehouse;
        editor.SelectedSupplier = editor.Suppliers[0];
        editor.SelectedType = editor.Types.Single(option => option.Value == MovementType.WriteOff);
        await Render(view, "invoices-write-off-form", 950, 820);
        Check(!editor.ShowsSupplier && supplierField.Visibility == Visibility.Collapsed && editor.SelectedSupplier is null,
            "selecting WriteOff hides and clears the supplier field");

        editor.QuantityText = (before + 1).ToString(CultureInfo.InvariantCulture);
        await editor.SaveCommand.ExecuteAsync(null);
        await Render(view, "invoices-excess-write-off-error", 950, 820);
        var serviceError = (TextBlock)editorView.FindName("SaveErrorText");
        var query = employee.GetRequiredService<StockMovementQueryService>();
        Check(ReferenceEquals(editor, list.Editor) && serviceError.Text == "Недостатньо товару на складі" &&
            Quantity() == before && query.Search(new StockMovementFilter(receiptNumber)).Items.Count == 0,
            "over-stock write-off displays the real service error under the form without persisting or crashing");

        editor.SelectedType = editor.Types.Single(option => option.Value == MovementType.Receipt);
        editor.QuantityText = "0";
        await editor.SaveCommand.ExecuteAsync(null);
        Check(editor.ErrorMessage == "Кількість має бути більшою за 0", "invoice editor rejects zero quantity");
        editor.QuantityText = "5";
        await editor.SaveCommand.ExecuteAsync(null);
        Check(editor.ErrorMessage == "Для надходження потрібно обрати постачальника", "receipt cannot be saved without a supplier");
        editor.SelectedSupplier = editor.Suppliers[0];
        editor.Note = "SE7-5 receipt";
        await editor.SaveCommand.ExecuteAsync(null);
        Check(list.Editor is null && list.IsListOpen && list.Movements.Any(row => row.Number == receiptNumber &&
            row.Data.Type == MovementType.Receipt && row.ResponsibleName == currentUser.FullName && row.HasSupplier) &&
            Quantity() == before + 5, "receipt commits, increases the actual stock and refreshes the list after closing");

        await list.CreateCommand.ExecuteAsync(null);
        var writeOff = list.Editor ?? throw new InvalidOperationException(list.ListError ?? "Invoice editor was not reopened.");
        var writeOffNumber = writeOff.Number;
        Check(writeOffNumber != receiptNumber, "a second form receives a different invoice number");
        writeOff.Date = day;
        writeOff.SelectedProduct = writeOff.Products.Single(p => p.Id == product.Id);
        writeOff.SelectedWarehouse = writeOff.Warehouses.Single(w => w.Id == warehouse.Id);
        writeOff.SelectedType = writeOff.Types.Single(option => option.Value == MovementType.WriteOff);
        writeOff.QuantityText = "2";
        await writeOff.SaveCommand.ExecuteAsync(null);
        Check(list.Editor is null && list.Movements.Any(row => row.Number == writeOffNumber &&
            row.Data.Type == MovementType.WriteOff && !row.HasSupplier) && Quantity() == before + 3,
            "write-off commits without supplier, decreases actual stock and refreshes the list");

        list.DateFrom = list.DateTo = day;
        list.FilterType = MovementType.Receipt;
        list.FilterWarehouseId = warehouse.Id;
        await CheckAutomaticFilter(() => list.Movements.Any(row => row.Number == receiptNumber) &&
            list.Movements.All(row => row.Number != writeOffNumber && row.Date.Date == day &&
                row.Data.Type == MovementType.Receipt && row.WarehouseName == warehouse.Name),
            "invoice filters apply automatically and combine date, type and warehouse against PostgreSQL");
        await list.ClearFiltersCommand.ExecuteAsync(null);
        list.SearchNumber = writeOffNumber.ToLowerInvariant();
        await CheckAutomaticFilter(() => list.Movements.Count == 1 && list.Movements[0].Number == writeOffNumber,
            "invoice number search is automatic and case-insensitive");
        list.SearchNumber = "SE7-5-absent-" + Guid.NewGuid().ToString("N");
        await CheckAutomaticFilter(() => list.Movements.Count == 0 && list.EmptyMessage == "За обраними фільтрами накладних не знайдено." &&
            list.ListError is null, "empty search shows its own message, not an error");
        await Render(view, "invoices-empty-results");

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
