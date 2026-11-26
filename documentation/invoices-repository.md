# SE7-2: репозиторій рухів товару

Код входить у звичайний `StockFlow.DataAccess`, тести — у `StockFlow.Tests`.
Він використовує production `StockMovement` із PR #139 та єдиний командний
`Stock` із Domain. Прототипний test project і тимчасовий Stock видалені доменним PR.

## Контракт і файли

- `src/StockFlow.DataAccess/IStockMovementRepository.cs`:
  `int Add(StockMovement movement, IDbTransaction transaction)`.
- `src/StockFlow.DataAccess/StockMovementRepository.cs`: параметризований
  `INSERT` дев'яти колонок із `RETURNING id`.
- `src/StockFlow.DataAccess/DuplicateNumberException.cs`: очищена від деталей
  провайдера помилка дубліката номера.
- `tests/StockFlow.Tests/Repository/`: 23 unit-тести та recording-фейки ADO.NET.

Метод використовує виключно підключення переданої транзакції. Репозиторій
створює й звільняє власну команду, але не відкриває, не закриває й не звільняє
передане підключення/транзакцію та не виконує commit або rollback.
Повертає доданий id, не змінюючи вхідний доменний об'єкт.

`StockMovementTypeColumn` задає зіставлення `Receipt` ↔ `receipt` і
`WriteOff` ↔ `write_off` для запису та читання. Невідоме значення відхиляється.
`supplier_id` та `note` передаються як типізовані
параметри з `DBNull.Value`, якщо значення відсутнє. Решту бізнес-правил перевіряє
спільний домен через сервіс; репозиторій не дублює їх.

## Сумісність зі злитою схемою

Звірено з `database/01_schema.sql` у main `0be1541`:
`stock_movements` містить `number`, `type`, `product_id`, `warehouse_id`,
`supplier_id`, `user_id`, `quantity`, `movement_date`, `note`.
Унікальність `number` має PostgreSQL constraint `stock_movements_number_key`.
Лише SQLSTATE `23505` разом із цим точним constraint стає `DuplicateNumberException`;
помилки інших унікальних ключів та `40001` передаються сервісу для окремої обробки.
Первинний виняток не додається до помилки номера, щоб не показати SQL/параметри.

`movement_date TIMESTAMP` — без часового поясу. Явний `DbType.DateTime2` обирає
цей тип у Npgsql; `DateTimeKind.Unspecified` зберігає wall-clock поля без
перетворення часового поясу. Це відповідає
[офіційному зіставленню Npgsql](https://www.npgsql.org/doc/types/basic.html).

## Перевірки для #225

```sh
dotnet restore StockFlow.sln
dotnet format StockFlow.sln --verify-no-changes --no-restore
dotnet build StockFlow.sln -c Release --no-restore -warnaserror
dotnet test tests/StockFlow.Tests/StockFlow.Tests.csproj -c Release --no-build
dotnet run --project tests/StockFlow.DatabaseChecks -c Release
```

23 тести репозиторію перевіряють передачу всіх значень, SQL-параметризацію,
типи/NULL, `RETURNING id`, явне зіставлення enum, точну класифікацію SQLSTATE,
дотримання транзакції та звільнення лише власної команди на успіху й помилці.

`InvoiceDatabaseChecks.cs` запускається в job `database-checks` і перевіряє
реальні `INSERT`, `CHECK`, `FK`, `RESTRICT`, унікальність, обидва типи,
невідомий тип, точність дати та `NULL`. Використовується лише база з префіксом
`stockflow_isolated_` на loopback, підготовлена `database/01_schema.sql` і
`database/02_seed.sql`; рядок підключення читається зі `STOCKFLOW_TEST_CONNECTION`.

Репозиторій не вмикає створення накладних: сервіс, форма й фінальна перевірка
надходження та списання (issues #30/#32) ідуть окремо.
