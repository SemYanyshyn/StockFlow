# MA-8: редагування та видалення постачальників

Base: main. Branch: feature/suppliers-edit-delete.
Related issues: #159 (US-19), #160 (US-20).

Додано параметризовані Update/Delete, спільну валідацію, нормалізацію необов’язкових контактів у NULL та перевірку ролі в SupplierService. Форма редагування використовує копію значень; скасування не змінює дані. Видалення потребує IConfirmationService, FK 23503 перетворюється на українське повідомлення. Після змін пошук залишається актуальним.

Перевірено: restore, Release build (0 warnings/errors), 292 unit-тести без PostgreSQL. Перевірки реальної БД та Windows описано у спільному звіті MA-8–MA-10.

```powershell
dotnet restore StockFlow.sln
dotnet build StockFlow.sln -c Release --no-restore
dotnet test tests/StockFlow.Tests/StockFlow.Tests.csproj -c Release --no-build
```
