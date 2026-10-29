# SE-3: репозиторій рухів товару

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

`Receipt` відображається у `receipt`, `WriteOff` — у `write_off` явним switch;
невідомий enum відхиляється. `supplier_id` та `note` передаються як типізовані
параметри з `DBNull.Value`, якщо значення відсутнє. Решту бізнес-правил перевіряє
спільний домен через сервіс; репозиторій не дублює їх.

## Сумісність зі злитою схемою

Звірено з `database/01_schema.sql` у main `723e656`:
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

## Перевірки

```sh
export PATH="/private/tmp/stockflow-dotnet8:$PATH"
dotnet restore StockFlow.sln
dotnet build StockFlow.sln -c Release --no-restore
dotnet test tests/StockFlow.Tests/StockFlow.Tests.csproj -c Release --no-build --no-restore
dotnet test tests/StockFlow.Tests/StockFlow.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~StockMovementRepositoryTests
```

Фактично виконано на macOS ARM64, SDK .NET 8.0.427: restore та Release build
успішні, 0 warnings / 0 errors. У звичайному test project виконано **211 тестів**:
146 наявних командних/інфраструктурних +42 доменних +23 репозиторію; 0 failed,
0 skipped. Окремий фільтр StockMovementRepositoryTests виконав 23/23.

23 тести репозиторію перевіряють передачу всіх значень, SQL-параметризацію,
типи/NULL, `RETURNING id`, явне зіставлення enum, точну класифікацію SQLSTATE,
дотримання транзакції та звільнення лише власної команди на успіху й помилці.
Recording-фейки не доводять правильність виконання SQL, ізоляцію або атомарність
PostgreSQL. Реальні SQL-перевірки запускаються окремо тільки в новій локальній
тестовій БД; результати й межі перевірки фіксуються у `stage6-semen.md`.

PR #140 спирається на `feature/invoices-domain-rules` (PR #139), тому diff не
повторює домен. `Part of #128`, `Part of #30`, `Part of #32`; повні історії з
формою цим репозиторієм ще не завершені. PR лишається Draft до остаточної
перевірки CI й узгодження командної інтеграції; він не зливався.

CI на main `723e656` фактично успішний (run 40968466801, job build-and-test).
Наявний workflow виконується для push/main та PR із base main. Для цього PR з
feature-base CI автоматично не запускається; успіх main не є перевіркою head PR.
